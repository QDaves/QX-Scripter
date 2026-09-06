using System.Text.Json;
using Qx.Game.Application;
using Qx.Hosting;
using Qx.Interception;
using Qx.Interception.GEarth;

namespace Qx.App;

internal static class ApplicationCommands
{
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);

    public static bool IsCommand(string[] args) =>
        args.Length != 0 && string.Equals(args[0], "app", StringComparison.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, event_args) =>
        {
            event_args.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args.Length < 2)
                return Usage(Console.Error, 2);

            return args[1].ToLowerInvariant() switch
            {
                "list" => await ListAsync(args).ConfigureAwait(false),
                "describe" => await DescribeAsync(args).ConfigureAwait(false),
                "invoke" => await InvokeAsync(args, cancellation.Token).ConfigureAwait(false),
                "watch" => await WatchAsync(args, cancellation.Token).ConfigureAwait(false),
                "session" => await SessionAsync(args, cancellation.Token).ConfigureAwait(false),
                "help" or "--help" or "-h" => Usage(Console.Out, 0),
                _ => Usage(Console.Error, 2)
            };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 130;
        }
        catch (ApplicationUnavailableException error)
        {
            Console.Error.WriteLine(ApplicationJson.Serialize(new
            {
                error = "application_unavailable",
                message = error.Message,
                details = AvailabilityDetails(error)
            }));
            return 1;
        }
        catch (KeyNotFoundException error)
        {
            Console.Error.WriteLine(error.Message);
            return 2;
        }
        catch (JsonException error)
        {
            Console.Error.WriteLine(error.Message);
            return 2;
        }
        catch (ArgumentException error)
        {
            Console.Error.WriteLine(error.Message);
            return 2;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancel;
        }
    }

    internal static object[] ListMembers(IApplicationRuntime application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application.Members
            .Where(descriptor => descriptor.Exposure.HasFlag(ApplicationExposure.Cli))
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .Select(descriptor => ApplicationJson.Describe(application.Describe(descriptor.Id)))
            .ToArray();
    }

    internal static object DescribeMember(IApplicationRuntime application, string id)
    {
        ApplicationDescriptor descriptor = CliMember(application, id);
        return ApplicationJson.Describe(application.Describe(descriptor.Id));
    }

    internal static ApplicationDescriptor InvokableDescriptor(
        IApplicationRuntime application,
        string id)
    {
        ApplicationDescriptor descriptor = CliMember(application, id);
        if (descriptor.Kind is ApplicationMemberKind.Event || descriptor.RequestType is null)
            throw new ArgumentException($"Application member '{id}' cannot be invoked.", nameof(id));
        return descriptor;
    }

    internal static ApplicationDescriptor EventDescriptor(
        IApplicationRuntime application,
        string id)
    {
        ApplicationDescriptor descriptor = CliMember(application, id);
        if (descriptor.Kind is not ApplicationMemberKind.Event)
            throw new ArgumentException($"Application member '{id}' is not an event.", nameof(id));
        return descriptor;
    }

    internal static bool RequiresConnection(ApplicationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.RequiredStates.Contains(ApplicationStateKey.HotelConnected);
    }

    internal static async Task WaitForConnectionAsync(
        RuntimeHost runtime,
        CancellationToken cancellation_token)
    {
        if (runtime.Extension.Session is not null)
            return;

        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void ConnectionEstablished(Session _) => connected.TrySetResult();

        runtime.Extension.Connected += ConnectionEstablished;
        try
        {
            if (runtime.Extension.Session is not null)
                return;

            Task transport = runtime.TransportTask;
            Task cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellation_token);
            Task completed = await Task
                .WhenAny(connected.Task, transport, cancellation)
                .ConfigureAwait(false);
            if (ReferenceEquals(completed, cancellation))
                await cancellation.ConfigureAwait(false);
            if (ReferenceEquals(completed, connected.Task))
                return;

            await transport.ConfigureAwait(false);
            throw new InvalidOperationException(
                "The transport ended before a hotel connection was established.");
        }
        finally
        {
            runtime.Extension.Connected -= ConnectionEstablished;
        }
    }

    internal static object AvailabilityDetails(ApplicationUnavailableException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new
        {
            member = error.MemberId,
            availability = ApplicationJson.DescribeAvailability(error.Availability)
        };
    }

    private static async Task<int> ListAsync(string[] args)
    {
        RequireLength(args, 2, "QX app list");
        await using RuntimeHost runtime = CreateRuntime(args, offline: true);
        await WriteAsync(ListMembers(runtime.Application)).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> DescribeAsync(string[] args)
    {
        RequireLength(args, 3, "QX app describe <id>");
        await using RuntimeHost runtime = CreateRuntime(args, offline: true);
        await WriteAsync(DescribeMember(runtime.Application, args[2])).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> InvokeAsync(
        string[] args,
        CancellationToken cancellation_token)
    {
        RequireLength(args, 4, "QX app invoke <id> <json>");
        await using RuntimeHost runtime = CreateRuntime(args, offline: false);
        ApplicationDescriptor descriptor = InvokableDescriptor(runtime.Application, args[2]);
        if (descriptor.InvocationScope is ApplicationInvocationScope.Persistent)
        {
            throw new ArgumentException(
                $"Application member '{descriptor.Id}' requires a persistent runtime. Use 'QX app session'.",
                nameof(args));
        }
        using JsonDocument document = JsonDocument.Parse(args[3]);
        object request = ApplicationJson.Deserialize(document.RootElement, descriptor.RequestType!);

        await runtime.StartAsync(cancellation_token).ConfigureAwait(false);
        if (RequiresConnection(descriptor))
        {
            await WaitForConnectionAsync(runtime, cancellation_token).ConfigureAwait(false);
        }
        object? result = await runtime.Application
            .InvokeAsync(descriptor.Id, request, cancellation_token)
            .ConfigureAwait(false);
        await WriteAsync(result).ConfigureAwait(false);
        return 0;
    }

    private static async Task<int> WatchAsync(
        string[] args,
        CancellationToken cancellation_token)
    {
        RequireLength(args, 3, "QX app watch <id>");
        await using RuntimeHost runtime = CreateRuntime(args, offline: false);
        ApplicationDescriptor descriptor = EventDescriptor(runtime.Application, args[2]);
        await using var output = new BoundedNdjsonWriter(Console.Out);
        using CancellationTokenRegistration output_cancellation =
            cancellation_token.Register(output.Abort);
        using IDisposable subscription = runtime.Application.Subscribe(
            descriptor.Id,
            value =>
            {
                if (value is null)
                    output.Fail(new InvalidDataException($"Application event '{descriptor.Id}' published null."));
                else
                    output.TryWrite(value);
            });

        try
        {
            await runtime.StartAsync(cancellation_token).ConfigureAwait(false);
            Task cancellation = Task.Delay(Timeout.InfiniteTimeSpan, cancellation_token);
            Task completed = await Task
                .WhenAny(output.Failure, runtime.TransportTask, cancellation)
                .ConfigureAwait(false);
            if (ReferenceEquals(completed, output.Failure))
            {
                Exception failure = await output.Failure.ConfigureAwait(false);
                throw new IOException("The NDJSON output stream failed.", failure);
            }
            if (ReferenceEquals(completed, cancellation))
                await cancellation.ConfigureAwait(false);
            await runtime.TransportTask.ConfigureAwait(false);
            await output.CompleteAsync(OutputDrainTimeout).ConfigureAwait(false);
            return 0;
        }
        catch
        {
            output.Abort();
            throw;
        }
    }

    private static async Task<int> SessionAsync(
        string[] args,
        CancellationToken cancellation_token)
    {
        RequireLength(args, 2, "QX app session");
        await using RuntimeHost runtime = CreateRuntime(args, offline: false);
        await runtime.StartAsync(cancellation_token).ConfigureAwait(false);
        var session = new ApplicationSession(runtime, Console.In, Console.Out, cancellation_token);
        return await session.RunAsync().ConfigureAwait(false);
    }

    private static ApplicationDescriptor CliMember(IApplicationRuntime application, string id)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ApplicationDescriptor descriptor = application.Members.FirstOrDefault(
                member => string.Equals(member.Id, id, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Unknown application member '{id}'.");
        if (!descriptor.Exposure.HasFlag(ApplicationExposure.Cli))
            throw new ArgumentException(
                $"Application member '{id}' is not exposed to the CLI.",
                nameof(id));
        return descriptor;
    }

    private static RuntimeHost CreateRuntime(string[] args, bool offline)
    {
        GEarthOptions gearth = GEarthOptions.Parse(args, new GEarthOptions
        {
            Title = "QX",
            Author = "QDave",
            Description = "QX",
            Port = 9092
        });
        return new RuntimeHost(new RuntimeHostOptions
        {
            GEarth = gearth,
            EnableTransport = !offline,
            EnableFallbackCatalogs = !offline,
            EnableClientMonitoring = !offline,
            EnableMcp = false
        });
    }

    private static async Task WriteAsync(object? value)
    {
        await Console.Out
            .WriteLineAsync(ApplicationJson.Serialize(value))
            .ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
    }

    private static void RequireLength(string[] args, int length, string usage)
    {
        if (args.Length != length)
            throw new ArgumentException($"Usage: {usage}", nameof(args));
    }

    private static int Usage(TextWriter writer, int exit_code)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  QX app list");
        writer.WriteLine("  QX app describe <id>");
        writer.WriteLine("  QX app invoke <id> <json>");
        writer.WriteLine("  QX app watch <id>");
        writer.WriteLine("  QX app session");
        return exit_code;
    }
}
