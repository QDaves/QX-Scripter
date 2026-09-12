using Qx.Hosting;
using Qx.Scripting;

namespace Qx.App;

internal sealed class ScriptSession(RuntimeHost runtime, TextReader input, TextWriter output)
{
    private readonly object gate = new();
    private CancellationTokenSource? script_cancellation;
    private Task script_task = Task.CompletedTask;
    private string? last_file;
    private string script_state = "Ready";

    public async Task RunAsync(string? initial_file, CancellationToken cancellation_token)
    {
        output.WriteLine("Commands: run <file>, rerun, eval <code>, check <file>, scripts, status, stop, wait, help, exit");
        output.WriteLine("The session stays connected between scripts. Ctrl+C stops a script; when idle it exits.");
        try
        {
            if (initial_file is not null)
            {
                try
                {
                    Start(initial_file, null, cancellation_token);
                }
                catch (Exception error)
                {
                    output.WriteLine(error.Message);
                }
            }
            while (!cancellation_token.IsCancellationRequested)
            {
                if (!Console.IsInputRedirected)
                    output.Write("qx> ");
                Task<string?> read = Task.Run(input.ReadLine, CancellationToken.None);
                Task completed = await Task.WhenAny(read, runtime.TransportTask).WaitAsync(cancellation_token);
                if (completed == runtime.TransportTask)
                {
                    await runtime.TransportTask;
                    break;
                }
                string? line = await read;
                if (line is null)
                    break;
                line = line.Trim();
                if (line.Length == 0)
                    continue;
                int separator = line.IndexOfAny([' ', '\t']);
                string command = (separator < 0 ? line : line[..separator]).ToLowerInvariant();
                string argument = separator < 0 ? "" : line[(separator + 1)..].Trim();
                try
                {
                    if (command is not ("run" or "eval" or "check") && argument.Length != 0)
                        throw new ArgumentException($"'{command}' does not take arguments.");
                    switch (command)
                    {
                        case "run":
                            Start(FileArgument(argument), null, cancellation_token);
                            break;
                        case "rerun":
                            Start(last_file ?? throw new InvalidOperationException("No script file has been run yet."), null, cancellation_token);
                            break;
                        case "eval":
                            ArgumentException.ThrowIfNullOrWhiteSpace(argument);
                            Start(null, argument, cancellation_token);
                            break;
                        case "check":
                            ScriptExecutionRequest request = await ReadScriptAsync(FileArgument(argument), cancellation_token);
                            var diagnostics = await Task.Run(() => ScriptEngine.Compile(request.Code, request.FileName), cancellation_token);
                            foreach (var diagnostic in diagnostics)
                                output.WriteLine(diagnostic);
                            output.WriteLine(diagnostics.Any(value => value.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                                ? "Compilation failed." : "Compilation successful.");
                            break;
                        case "scripts":
                            foreach (string file in runtime.McpHost.ListScripts())
                                output.WriteLine(file + ".csx");
                            break;
                        case "status":
                            var connection = runtime.Extension;
                            output.WriteLine($"G-Earth: {(connection.IsInterceptorConnected ? "connected" : "disconnected")}");
                            output.WriteLine($"Hotel: {connection.Session?.Client.ToString() ?? "disconnected"}");
                            output.WriteLine(runtime.Game.Room.Capture(room =>
                                $"Room: {room.RoomId} ({room.State}), {room.Avatars.Count()} avatars, {room.FloorItems.Count()} floor items, {room.WallItems.Count()} wall items"));
                            lock (gate)
                                output.WriteLine($"Script: {script_state}");
                            break;
                        case "stop":
                            output.WriteLine(Stop() ? "Stopping script." : "No script is running.");
                            break;
                        case "wait":
                            await script_task.WaitAsync(cancellation_token);
                            break;
                        case "help":
                            output.WriteLine("run <file> starts a .csx file; rerun reads the last file again. Paths with spaces may be quoted.");
                            output.WriteLine("eval <code> runs inline C#. check <file> compiles without running. scripts lists the script library.");
                            output.WriteLine("status shows connection, room and script state. stop cancels only the script. wait waits for it to finish.");
                            output.WriteLine("exit closes the host. Enter a room once after connecting; later runs reuse its live state.");
                            break;
                        case "exit" or "quit":
                            return;
                        default:
                            output.WriteLine($"Unknown command '{command}'. Type help for commands.");
                            break;
                    }
                }
                catch (Exception error) when (error is not OperationCanceledException || !cancellation_token.IsCancellationRequested)
                {
                    output.WriteLine(error.Message);
                }
            }
        }
        finally
        {
            Stop();
            await script_task;
        }
    }

    public bool Stop()
    {
        lock (gate)
        {
            if (script_cancellation is null)
                return false;
            script_state = "Stopping";
            script_cancellation.Cancel();
            return true;
        }
    }

    private void Start(string? file, string? code, CancellationToken cancellation_token)
    {
        lock (gate)
        {
            if (script_cancellation is not null)
                throw new InvalidOperationException("A script is running. Stop it before starting another.");
            if (file is not null)
            {
                file = ResolveFile(file);
                if (!File.Exists(file))
                    throw new FileNotFoundException("Script file not found.", file);
                last_file = file;
            }
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
            script_cancellation = cancellation;
            script_state = "Waiting for hotel connection";
            script_task = Task.Run(() => RunScriptAsync(file, code, cancellation.Token), CancellationToken.None);
        }
    }

    private async Task RunScriptAsync(string? file, string? code, CancellationToken cancellation_token)
    {
        try
        {
            ScriptExecutionRequest request = file is null
                ? new ScriptExecutionRequest { Code = code!, SourceIdentity = "cli:eval", FileName = "eval.csx" }
                : await ReadScriptAsync(file, cancellation_token);
            ScriptExecutionResult result = await RunAsync(runtime, request with
            {
                OutputWritten = output.WriteLine,
                StateChanged = state =>
                {
                    lock (gate)
                        script_state = state.ToString();
                }
            }, cancellation_token);
            foreach (ScriptExecutionError error in result.Errors)
                output.WriteLine(error.Format());
            output.WriteLine($"{Path.GetFileName(request.FileName)}: {result.State} ({result.RuntimeMs:0} ms)");
        }
        catch (OperationCanceledException) when (cancellation_token.IsCancellationRequested)
        {
            output.WriteLine("Script stopped.");
        }
        catch (Exception error)
        {
            output.WriteLine($"Script failed: {error.Message}");
        }
        finally
        {
            lock (gate)
            {
                script_cancellation!.Dispose();
                script_cancellation = null;
                script_state = "Ready";
            }
        }
    }

    private static string FileArgument(string argument)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);
        if (argument[0] is not ('"' or '\''))
            return argument;
        if (argument.Length < 2 || argument[^1] != argument[0])
            throw new ArgumentException("The script path has an unclosed quote.");
        return argument[1..^1];
    }

    private static string ResolveFile(string file)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        string path = Path.GetFullPath(file);
        if (File.Exists(path) || Path.IsPathRooted(file))
            return path;
        return Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QX Scripter", "scripts", file));
    }

    internal static async Task<ScriptExecutionRequest> ReadScriptAsync(string file, CancellationToken cancellation_token)
    {
        string path = ResolveFile(file);
        return new ScriptExecutionRequest
        {
            Code = await File.ReadAllTextAsync(path, cancellation_token),
            SourceIdentity = path,
            FileName = path
        };
    }

    internal static async Task<ScriptExecutionResult> RunAsync(
        RuntimeHost runtime, ScriptExecutionRequest request, CancellationToken cancellation_token)
    {
        using var session_cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation_token);
        bool finished = false;
        void Disconnected()
        {
            lock (session_cancellation)
            {
                if (!finished)
                    session_cancellation.Cancel();
            }
        }
        runtime.Extension.Disconnected += Disconnected;
        try
        {
            await ApplicationCommands.WaitForConnectionAsync(runtime, session_cancellation.Token);
            return await runtime.ScriptExecution.RunAsync(request, session_cancellation.Token);
        }
        catch (OperationCanceledException) when (session_cancellation.IsCancellationRequested && !cancellation_token.IsCancellationRequested)
        {
            request.StateChanged?.Invoke(ScriptRunState.Stopped);
            return new ScriptExecutionResult(request.SourceIdentity, request.FileName,
                ScriptRunState.Stopped, false, false, false, 0, "", []);
        }
        finally
        {
            runtime.Extension.Disconnected -= Disconnected;
            lock (session_cancellation)
                finished = true;
        }
    }
}
