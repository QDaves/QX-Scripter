using Qx;
using Qx.Diagnostics;
using Qx.Game;
using Qx.Game.Application;
using Qx.Hosting;
using Qx.Interception.GEarth;
using Qx.Scripting;

if (Qx.App.ApplicationCommands.IsCommand(args))
{
    return await Qx.App.ApplicationCommands.RunAsync(args);
}

return await Run(args);

static string? ArgValue(string[] args, string name)
{
    int index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

static async Task<int> Run(string[] args)
{
    if (args.Any(value => value is "help" or "--help" or "-h"))
    {
        Console.WriteLine("QX [shell] [-p <port>] [-q] [--script <file>] [--headless] [--packets]");
        Console.WriteLine("shell keeps one connection and room cache for run, rerun, eval, check, scripts, status and stop.");
        Console.WriteLine("An interactive terminal opens the shell by default. --headless runs only the host and optional startup script.");
        Console.WriteLine("--script runs once after connecting, never automatically again after reconnecting.");
        Console.WriteLine("--packets enables packet logging in the shell; -q disables packet logging in all modes.");
        Console.WriteLine("QX app help lists JSON automation commands, including the persistent app session.");
        return 0;
    }
    try
    {
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument is "shell" && index == 0 || argument is "-q" or "--quiet" or "--headless" or "--packets")
                continue;
            if (argument is not ("-p" or "-f" or "-c" or "--script") || ++index == args.Length)
                throw new ArgumentException($"Unknown or incomplete option '{argument}'. Use QX --help.");
            if (argument == "-p" && (!int.TryParse(args[index], out int port) || port is < 1 or > 65535))
                throw new ArgumentException("The G-Earth port must be between 1 and 65535.");
        }
        if (args.Contains("shell") && args.Contains("--headless"))
            throw new ArgumentException("Use either shell or --headless, not both.");
    }
    catch (ArgumentException error)
    {
        Console.Error.WriteLine(error.Message);
        return 2;
    }

    GEarthOptions options = GEarthOptions.Parse(args, new GEarthOptions
    {
        Title = "QX",
        Author = "QDave",
        Description = "QX",
        Port = 9092
    });

    bool interactive = args.FirstOrDefault() == "shell" ||
        (!Console.IsInputRedirected && !options.IsLaunchedByGEarth && !args.Contains("--headless"));
    bool quiet = args.Contains("--quiet") || args.Contains("-q") || interactive && !args.Contains("--packets");
    string? script_path = ArgValue(args, "--script");
    options.SearchPorts = !args.Contains("-p") && !options.IsLaunchedByGEarth;

    Diag.Enabled = true;
    Diag.MinLevel = DiagLevel.Info;
    Diag.Emitted += (level, message, category) =>
    {
        string tag = category is null ? "" : category + " ";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level} {tag}{message}");
    };

    using var cts = new CancellationTokenSource();
    Qx.App.ScriptSession? shell = null;
    ConsoleCancelEventHandler cancel = (_, e) =>
    {
        e.Cancel = true;
        if (shell?.Stop() != true)
            cts.Cancel();
    };

    string scripts_directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "scripts");
    await using var runtime = new RuntimeHost(new RuntimeHostOptions
    {
        GEarth = options,
        ScriptsDirectory = scripts_directory,
        ReconnectTransport = !options.IsLaunchedByGEarth
    });
    GEarthExtension extension = runtime.Extension;
    GameState game = runtime.Game;

    game.Room.Entered += () => Diag.Info($"Entered room {game.Room.RoomId} (owner: {game.Room.IsOwner})");
    game.Room.Left += () => Diag.Info("Left room");
    long logged_profile_generation = -1;
    using IDisposable profile_subscription = runtime.Application.Subscribe<ProfileChanged>(
        ApplicationMemberIds.ProfileChanged,
        change =>
        {
            if (change.Kind is not ProfileChangeKind.Identity ||
                change.State.Identity is not { } identity ||
                logged_profile_generation == change.State.Generation)
            {
                return;
            }

            logged_profile_generation = change.State.Generation;
            Diag.Info($"Signed in as {identity.Name} (#{identity.Id})");
        });
    long logged_inventory_generation = -1;
    using IDisposable inventory_subscription = runtime.Application.Subscribe<InventoryFurniChanged>(
        ApplicationMemberIds.InventoryFurniChanged,
        change =>
        {
            if (change.Kind is not InventoryChangeKind.Loaded ||
                logged_inventory_generation == change.LoadGeneration)
            {
                return;
            }

            InventoryFurniPage page = runtime.Application
                .Invoke<InventoryFurniPageRequest, InventoryFurniPage>(
                    ApplicationMemberIds.InventoryFurniList,
                    new InventoryFurniPageRequest(Limit: 1));
            if (page.SessionGeneration != change.SessionGeneration ||
                page.Revision != change.Revision ||
                page.InventoryRevision != change.SnapshotRevision)
            {
                return;
            }

            logged_inventory_generation = change.LoadGeneration;
            Diag.Info($"Inventory loaded ({page.Total} items)");
        });
    game.Friends.Loaded += () => Diag.Info($"Friends loaded ({game.Friends.Friends.Count})");

    extension.Connected += session => Diag.Info($"Connected: {session.Client} {session.HotelVersion} @ {session.Host}:{session.Port}");
    extension.Disconnected += () => Diag.Info("Disconnected from hotel");
    extension.Initialized += () => Diag.Info("Extension initialized");

    if (!quiet)
    {
        extension.Intercepted += intercept =>
        {
            string name = runtime.Messages.TryGetIdentifier(intercept.Packet.Header, out var identifier)
                ? identifier.Name
                : $"#{intercept.Packet.Header.Value}";
            string arrow = intercept.Direction == Direction.In ? "<-" : "->";
            Diag.Info($"{arrow} {name} ({intercept.Packet.Length}b)", "packet");
        };
    }

    Console.WriteLine($"QX {options.Version} - G-Earth extension on port {options.Port}.");

    Task script_task = Task.CompletedTask;
    Console.CancelKeyPress += cancel;
    try
    {
        await runtime.StartAsync(cts.Token);
        if (interactive)
        {
            shell = new Qx.App.ScriptSession(runtime, Console.In, Console.Out);
            await shell.RunAsync(script_path, cts.Token);
        }
        else
        {
            Console.WriteLine("Ctrl+C to quit. Use QX shell to run more scripts without restarting the host.");
            if (script_path is not null)
                script_task = RunScriptAsync(runtime, script_path, cts.Token);
            await runtime.TransportTask.WaitAsync(cts.Token);
        }
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Fatal: " + ex.Message);
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
        cts.Cancel();
        await script_task;
    }
    return 0;
}

static async Task RunScriptAsync(
    RuntimeHost runtime,
    string script_path,
    CancellationToken cancellation_token)
{
    try
    {
        ScriptExecutionRequest request = await Qx.App.ScriptSession.ReadScriptAsync(script_path, cancellation_token);
        string path = request.FileName;
        Diag.Info($"Running script {Path.GetFileName(path)}", "script");
        ScriptExecutionResult result = await Qx.App.ScriptSession.RunAsync(runtime, request with
        {
            OutputWritten = message => Diag.Info(message, "script")
        }, cancellation_token);

        if (result.AlreadyRunning)
        {
            Diag.Warn($"Script {Path.GetFileName(path)} is already running", "script");
            return;
        }
        foreach (ScriptExecutionError error in result.Errors)
            Diag.Error(error.Format(), "script");
        if (result.State == ScriptRunState.Finished)
            Diag.Info("Script finished", "script");
        else if (result.State == ScriptRunState.Stopped)
            Diag.Info("Script stopped", "script");
        else if (result.State == ScriptRunState.Faulted)
            Diag.Error($"Script failed with {result.Errors.Length} error(s)", "script");
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        Diag.Error($"Script error: {ex}", "script");
    }
}
