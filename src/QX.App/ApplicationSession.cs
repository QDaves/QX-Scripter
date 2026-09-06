using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Qx.Game.Application;
using Qx.Hosting;

namespace Qx.App;

internal sealed class ApplicationSession
{
    private const int MaxLineCharacters = 1_048_576;
    private const int MaxActiveRequests = 64;
    private const int MaxSubscriptions = 64;
    private const int InputCapacity = 64;
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);
    private readonly RuntimeHost runtime;
    private readonly SessionInput input;
    private readonly CancellationToken outer_cancellation;
    private readonly CancellationTokenSource lifetime;
    private readonly SessionOutput output;
    private readonly CancellationTokenRegistration output_cancellation;
    private readonly ConcurrentDictionary<string, ActiveRequest> active_requests =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SessionSubscription> subscriptions =
        new(StringComparer.Ordinal);
    private readonly HashSet<Task> request_tasks = [];
    private readonly object request_tasks_gate = new();
    private long next_subscription;

    public ApplicationSession(
        RuntimeHost runtime,
        TextReader input,
        TextWriter output,
        CancellationToken cancellation_token)
    {
        this.runtime = runtime;
        this.input = new SessionInput(input, MaxLineCharacters, InputCapacity);
        outer_cancellation = cancellation_token;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
        this.output = new SessionOutput(new BoundedNdjsonWriter(output));
        output_cancellation = cancellation_token.Register(this.output.Abort);
    }

    public async Task<int> RunAsync()
    {
        try
        {
            bool running = true;
            while (running && !lifetime.IsCancellationRequested)
            {
                Task<LineReadResult> read = input.ReadAsync(lifetime.Token).AsTask();
                Task completed = await Task.WhenAny(
                        output.Failure,
                        input.Failure,
                        runtime.TransportTask,
                        read)
                    .ConfigureAwait(false);
                if (ReferenceEquals(completed, runtime.TransportTask))
                {
                    Cancel();
                    input.Stop();
                    await IgnoreReadAsync(read).ConfigureAwait(false);
                    await runtime.TransportTask.ConfigureAwait(false);
                    break;
                }
                if (ReferenceEquals(completed, output.Failure))
                {
                    Cancel();
                    input.Stop();
                    await IgnoreReadAsync(read).ConfigureAwait(false);
                    Exception error = await output.Failure.ConfigureAwait(false);
                    throw new IOException("The NDJSON output stream failed.", error);
                }
                if (ReferenceEquals(completed, input.Failure))
                {
                    Cancel();
                    input.Stop();
                    await IgnoreReadAsync(read).ConfigureAwait(false);
                    Exception error = await input.Failure.ConfigureAwait(false);
                    output.Error(null, "input_failed", error.Message);
                    throw new IOException("The NDJSON input stream failed.", error);
                }

                LineReadResult result = await read.ConfigureAwait(false);
                if (result.EndOfInput)
                    break;
                if (result.TooLarge)
                {
                    output.Error(
                        null,
                        "request_too_large",
                        $"A session request cannot exceed {MaxLineCharacters} characters.");
                    continue;
                }
                running = Dispatch(result.Line!);
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            await StopAsync().ConfigureAwait(false);
        }

        if (output.FailureException is { } failure)
            throw new IOException("The NDJSON output stream failed.", failure);
        outer_cancellation.ThrowIfCancellationRequested();
        return 0;
    }

    private bool Dispatch(string line)
    {
        SessionRequest request;
        try
        {
            request = SessionRequest.Parse(line);
        }
        catch (SessionProtocolException error)
        {
            output.Error(error.RequestId, error.Code, error.Message);
            return true;
        }

        if (active_requests.ContainsKey(request.Id))
        {
            output.Error(
                request.Id,
                "duplicate_request_id",
                $"Request id '{request.Id}' is already active.");
            return true;
        }

        try
        {
            return request.Method switch
            {
                "health" => ReportHealth(request),
                "cancel_request" => CancelActiveRequest(request),
                "subscribe" => Subscribe(request),
                "unsubscribe" => Unsubscribe(request),
                "close" => Close(request),
                _ => StartRequest(request)
            };
        }
        catch (Exception error)
        {
            WriteError(request.Id, error);
            return true;
        }
    }

    private bool ReportHealth(SessionRequest request)
    {
        output.Success(request.Id, new
        {
            protocol = "qx.application.ndjson",
            version = 1,
            active_requests = active_requests.Count,
            subscriptions = subscriptions.Count
        });
        return true;
    }

    private bool CancelActiveRequest(SessionRequest request)
    {
        if (!active_requests.TryGetValue(request.Target!, out ActiveRequest? active))
        {
            output.Error(
                request.Id,
                "request_not_active",
                $"Request '{request.Target}' is not active.");
            return true;
        }

        active.Cancel();
        output.Success(request.Id, new
        {
            request_id = request.Target,
            cancelled = true
        });
        return true;
    }

    private bool Subscribe(SessionRequest request)
    {
        if (subscriptions.Count >= MaxSubscriptions)
        {
            output.Error(
                request.Id,
                "capacity_exceeded",
                $"A session supports at most {MaxSubscriptions} active subscriptions.");
            return true;
        }

        ApplicationDescriptor descriptor = ApplicationCommands.EventDescriptor(
            runtime.Application,
            request.Member!);
        string subscription_id = $"subscription-{Interlocked.Increment(ref next_subscription)}";
        var subscription = new SessionSubscription(output, subscription_id, descriptor.Id);
        subscription.Attach(runtime.Application.Subscribe(descriptor.Id, subscription.Publish));
        if (!subscriptions.TryAdd(subscription_id, subscription))
        {
            subscription.Dispose();
            throw new InvalidOperationException($"Subscription id '{subscription_id}' already exists.");
        }

        subscription.Announce(request.Id);
        return true;
    }

    private bool Unsubscribe(SessionRequest request)
    {
        if (!subscriptions.TryRemove(request.Target!, out SessionSubscription? subscription))
        {
            output.Error(
                request.Id,
                "unknown_subscription",
                $"Subscription '{request.Target}' does not exist.");
            return true;
        }

        subscription.Dispose();
        output.Success(request.Id, new
        {
            subscription_id = request.Target,
            unsubscribed = true
        });
        return true;
    }

    private bool Close(SessionRequest request)
    {
        output.Success(request.Id, new { closed = true });
        return false;
    }

    private bool StartRequest(SessionRequest request)
    {
        if (active_requests.Count >= MaxActiveRequests)
        {
            output.Error(
                request.Id,
                "capacity_exceeded",
                $"A session supports at most {MaxActiveRequests} in-flight requests.");
            return true;
        }

        var active = new ActiveRequest(lifetime.Token);
        if (!active_requests.TryAdd(request.Id, active))
        {
            active.Dispose();
            output.Error(request.Id, "duplicate_request_id", $"Request id '{request.Id}' is active.");
            return true;
        }

        Task task;
        try
        {
            task = Task.Run(
                () => CompleteRequestAsync(request, active),
                CancellationToken.None);
        }
        catch
        {
            active_requests.TryRemove(request.Id, out _);
            active.Dispose();
            throw;
        }
        lock (request_tasks_gate)
            request_tasks.Add(task);
        _ = task.ContinueWith(
            completed =>
            {
                _ = completed.Exception;
                lock (request_tasks_gate)
                    request_tasks.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return true;
    }

    private async Task CompleteRequestAsync(SessionRequest request, ActiveRequest active)
    {
        try
        {
            active.Token.ThrowIfCancellationRequested();
            object? result = request.Method switch
            {
                "list" => ApplicationCommands.ListMembers(runtime.Application),
                "describe" => ApplicationCommands.DescribeMember(runtime.Application, request.Member!),
                "invoke" => await InvokeMemberAsync(request, active.Token).ConfigureAwait(false),
                _ => throw new InvalidOperationException($"Unsupported request method '{request.Method}'.")
            };
            output.Success(request.Id, result);
        }
        catch (OperationCanceledException) when (active.IsCancellationRequested)
        {
            output.Error(request.Id, "cancelled", $"Request '{request.Id}' was cancelled.");
        }
        catch (Exception error)
        {
            WriteError(request.Id, error);
        }
        finally
        {
            active_requests.TryRemove(request.Id, out _);
            active.Dispose();
        }
    }

    private async Task<object?> InvokeMemberAsync(
        SessionRequest request,
        CancellationToken cancellation_token)
    {
        ApplicationDescriptor descriptor = ApplicationCommands.InvokableDescriptor(
            runtime.Application,
            request.Member!);
        object arguments = ApplicationJson.Deserialize(request.Arguments!.Value, descriptor.RequestType!);
        if (ApplicationCommands.RequiresConnection(descriptor))
        {
            await ApplicationCommands
                .WaitForConnectionAsync(runtime, cancellation_token)
                .ConfigureAwait(false);
        }
        return await runtime.Application
            .InvokeAsync(descriptor.Id, arguments, cancellation_token)
            .ConfigureAwait(false);
    }

    private void WriteError(string request_id, Exception error)
    {
        switch (error)
        {
            case ApplicationUnavailableException unavailable:
                output.Error(
                    request_id,
                    "unavailable",
                    unavailable.Message,
                    ApplicationCommands.AvailabilityDetails(unavailable));
                break;
            case KeyNotFoundException:
                output.Error(request_id, "unknown_member", error.Message);
                break;
            case JsonException:
            case ArgumentException:
                output.Error(request_id, "invalid_arguments", error.Message);
                break;
            default:
                output.Error(request_id, "internal_error", error.Message);
                break;
        }
    }

    private async Task StopAsync()
    {
        input.Stop();
        Cancel();
        foreach ((string id, SessionSubscription subscription) in subscriptions.ToArray())
        {
            if (subscriptions.TryRemove(id, out _))
                subscription.Dispose();
        }
        foreach (ActiveRequest active in active_requests.Values)
            active.Cancel();

        Task[] tasks;
        lock (request_tasks_gate)
            tasks = request_tasks.ToArray();
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
        }

        Exception? completion_error = null;
        try
        {
            await output.CompleteAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            completion_error = error;
        }
        finally
        {
            output_cancellation.Dispose();
            lifetime.Dispose();
        }
        if (completion_error is not null && output.FailureException is null)
            throw completion_error;
    }

    private void Cancel()
    {
        try
        {
            lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static async Task IgnoreReadAsync(Task<LineReadResult> read)
    {
        try
        {
            await read.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class SessionInput
    {
        private readonly LimitedLineReader reader;
        private readonly Channel<LineReadResult> queue;
        private readonly CancellationTokenSource shutdown = new();
        private readonly TaskCompletionSource<Exception> failure =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task pump;
        private int stopped;

        public SessionInput(TextReader reader, int maximum_length, int capacity)
        {
            ArgumentNullException.ThrowIfNull(reader);
            if (maximum_length < 1)
                throw new ArgumentOutOfRangeException(nameof(maximum_length));
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            this.reader = new LimitedLineReader(reader, maximum_length);
            queue = Channel.CreateBounded<LineReadResult>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            pump = Task.Factory.StartNew(
                Pump,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            _ = pump.ContinueWith(
                completed => _ = completed.Exception,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public Task<Exception> Failure => failure.Task;

        public async ValueTask<LineReadResult> ReadAsync(CancellationToken cancellation_token)
        {
            while (await queue.Reader
                       .WaitToReadAsync(cancellation_token)
                       .ConfigureAwait(false))
            {
                if (queue.Reader.TryRead(out LineReadResult result))
                    return result;
            }
            return new LineReadResult(null, false, true);
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref stopped, 1) != 0)
                return;
            shutdown.Cancel();
            queue.Writer.TryComplete();
        }

        private void Pump()
        {
            try
            {
                while (Volatile.Read(ref stopped) == 0)
                {
                    LineReadResult result = reader.ReadLine();
                    if (result.EndOfInput)
                        return;
                    queue.Writer
                        .WriteAsync(result, shutdown.Token)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                }
            }
            catch (OperationCanceledException) when (Volatile.Read(ref stopped) != 0)
            {
            }
            catch (ChannelClosedException) when (Volatile.Read(ref stopped) != 0)
            {
            }
            catch (Exception error)
            {
                failure.TrySetResult(error);
            }
            finally
            {
                queue.Writer.TryComplete();
            }
        }
    }

    private sealed class LimitedLineReader(TextReader reader, int maximum_length)
    {
        private readonly char[] buffer = new char[4096];
        private int offset;
        private int length;

        public LineReadResult ReadLine()
        {
            StringBuilder? line = new();
            int line_length = 0;
            bool too_large = false;
            bool has_data = false;

            while (true)
            {
                if (offset == length)
                {
                    length = reader.Read(buffer, 0, buffer.Length);
                    offset = 0;
                    if (length == 0)
                    {
                        if (!has_data && !too_large)
                            return new LineReadResult(null, false, true);
                        return too_large
                            ? new LineReadResult(null, true, false)
                            : new LineReadResult(line!.ToString(), false, false);
                    }
                }

                int newline = Array.IndexOf(buffer, '\n', offset, length - offset);
                int end = newline >= 0 ? newline : length;
                int segment_length = end - offset;
                if (segment_length != 0)
                {
                    has_data = true;
                    if (!too_large)
                    {
                        if (segment_length > maximum_length - line_length)
                        {
                            too_large = true;
                            line = null;
                        }
                        else
                        {
                            line!.Append(buffer, offset, segment_length);
                            line_length += segment_length;
                        }
                    }
                }
                offset = end;

                if (newline < 0)
                    continue;

                offset++;
                if (too_large)
                    return new LineReadResult(null, true, false);
                if (line_length != 0 && line![line_length - 1] == '\r')
                    line.Length--;
                return new LineReadResult(line!.ToString(), false, false);
            }
        }
    }

    private readonly record struct LineReadResult(
        string? Line,
        bool TooLarge,
        bool EndOfInput);

    private sealed class ActiveRequest : IDisposable
    {
        private readonly CancellationTokenSource source;
        private readonly object gate = new();
        private bool disposed;

        public ActiveRequest(CancellationToken lifetime)
        {
            source = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            Token = source.Token;
        }

        public CancellationToken Token { get; }
        public bool IsCancellationRequested => Token.IsCancellationRequested;

        public void Cancel()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                source.Cancel();
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                source.Dispose();
            }
        }
    }

    private sealed class SessionSubscription(
        SessionOutput output,
        string subscription_id,
        string member) : IDisposable
    {
        private const int PendingCapacity = 64;
        private readonly object gate = new();
        private readonly Queue<object?> pending = [];
        private IDisposable? source;
        private bool announced;
        private bool disposed;

        public void Attach(IDisposable subscription)
        {
            ArgumentNullException.ThrowIfNull(subscription);
            bool reject;
            lock (gate)
            {
                if (source is not null)
                    throw new InvalidOperationException($"Subscription '{subscription_id}' is already attached.");
                reject = disposed;
                if (!reject)
                    source = subscription;
            }
            if (!reject)
                return;
            subscription.Dispose();
            throw new ObjectDisposedException(nameof(SessionSubscription));
        }

        public void Announce(string request_id)
        {
            output.Success(request_id, new
            {
                subscription_id,
                member
            });

            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                foreach (object? value in pending)
                    output.Event(subscription_id, member, value);
                pending.Clear();
                announced = true;
            }
        }

        public void Publish(object? value)
        {
            lock (gate)
            {
                if (disposed)
                    return;
                if (announced)
                {
                    output.Event(subscription_id, member, value);
                }
                else if (pending.Count < PendingCapacity)
                {
                    pending.Enqueue(value);
                }
                else
                {
                    output.Fail(new IOException(
                        $"Subscription '{subscription_id}' exceeded its pending event capacity."));
                }
            }
        }

        public void Dispose()
        {
            IDisposable? subscription;
            lock (gate)
            {
                if (disposed)
                    return;
                disposed = true;
                pending.Clear();
                subscription = source;
                source = null;
            }
            subscription?.Dispose();
        }
    }

    private sealed class SessionOutput(BoundedNdjsonWriter writer)
    {
        private readonly object event_gate = new();
        private long event_sequence;

        public Task<Exception> Failure => writer.Failure;
        public Exception? FailureException => writer.FailureException;

        public void Success(string id, object? result) => writer.Write(new
        {
            type = "response",
            id,
            ok = true,
            result
        });

        public void Error(string? id, string code, string message, object? details = null) => writer.Write(new
        {
            type = "response",
            id,
            ok = false,
            error = new
            {
                code,
                message,
                details
            }
        });

        public void Event(string subscription_id, string member, object? value)
        {
            lock (event_gate)
            {
                writer.TryWrite(new
                {
                    type = "event",
                    sequence = ++event_sequence,
                    subscription_id,
                    member,
                    data = value
                });
            }
        }

        public void Fail(Exception error) => writer.Fail(error);

        public void Abort() => writer.Abort();

        public Task CompleteAsync() => writer.CompleteAsync(OutputDrainTimeout);
    }

    private sealed record SessionRequest(
        string Id,
        string Method,
        string? Member,
        string? Target,
        JsonElement? Arguments)
    {
        public static SessionRequest Parse(string line)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException error)
            {
                throw new SessionProtocolException(null, "invalid_json", error.Message);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind is not JsonValueKind.Object)
                {
                    throw new SessionProtocolException(
                        null,
                        "invalid_request",
                        "A session request must be a JSON object.");
                }

                string? request_id = OptionalString(root, "id");
                if (string.IsNullOrWhiteSpace(request_id) || request_id.Length > 256)
                {
                    throw new SessionProtocolException(
                        request_id,
                        "invalid_request",
                        "Request 'id' must be a non-empty string with at most 256 characters.");
                }

                string method = RequiredString(root, "method", request_id);
                return method switch
                {
                    "list" or "health" or "close" => WithoutArguments(root, request_id, method),
                    "describe" or "subscribe" => WithMember(root, request_id, method),
                    "invoke" => Invocation(root, request_id),
                    "cancel_request" => WithTarget(root, request_id, method, "request_id"),
                    "unsubscribe" => WithTarget(root, request_id, method, "subscription_id"),
                    _ => throw new SessionProtocolException(
                        request_id,
                        "unknown_method",
                        $"Unknown session method '{method}'.")
                };
            }
        }

        private static SessionRequest WithoutArguments(JsonElement root, string id, string method)
        {
            ValidateProperties(root, id, "id", "method");
            return new SessionRequest(id, method, null, null, null);
        }

        private static SessionRequest WithMember(JsonElement root, string id, string method)
        {
            ValidateProperties(root, id, "id", "method", "member");
            return new SessionRequest(
                id,
                method,
                RequiredString(root, "member", id),
                null,
                null);
        }

        private static SessionRequest Invocation(JsonElement root, string id)
        {
            ValidateProperties(root, id, "id", "method", "member", "arguments");
            if (!root.TryGetProperty("arguments", out JsonElement arguments) ||
                arguments.ValueKind is not JsonValueKind.Object)
            {
                throw new SessionProtocolException(
                    id,
                    "invalid_request",
                    "Invoke requests require an object-valued 'arguments' property.");
            }
            return new SessionRequest(
                id,
                "invoke",
                RequiredString(root, "member", id),
                null,
                arguments.Clone());
        }

        private static SessionRequest WithTarget(
            JsonElement root,
            string id,
            string method,
            string property)
        {
            ValidateProperties(root, id, "id", "method", property);
            return new SessionRequest(
                id,
                method,
                null,
                RequiredString(root, property, id),
                null);
        }

        private static void ValidateProperties(
            JsonElement root,
            string request_id,
            params string[] allowed)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var accepted = new HashSet<string>(allowed, StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new SessionProtocolException(
                        request_id,
                        "invalid_request",
                        $"Property '{property.Name}' is duplicated.");
                }
                if (!accepted.Contains(property.Name))
                {
                    throw new SessionProtocolException(
                        request_id,
                        "invalid_request",
                        $"Property '{property.Name}' is not valid for this method.");
                }
            }
        }

        private static string RequiredString(
            JsonElement root,
            string property,
            string? request_id)
        {
            string? value = OptionalString(root, property);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new SessionProtocolException(
                    request_id,
                    "invalid_request",
                    $"Property '{property}' must be a non-empty string.");
            }
            return value;
        }

        private static string? OptionalString(JsonElement root, string property) =>
            root.TryGetProperty(property, out JsonElement value) &&
            value.ValueKind is JsonValueKind.String
                ? value.GetString()
                : null;
    }

    private sealed class SessionProtocolException(
        string? request_id,
        string code,
        string message) : Exception(message)
    {
        public string? RequestId { get; } = request_id;
        public string Code { get; } = code;
    }
}
