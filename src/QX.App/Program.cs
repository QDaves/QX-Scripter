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
    GEarthOptions options = GEarthOptions.Parse(args, new GEarthOptions
    {
        Title = "QX",
        Author = "QDave",
        Description = "QX",
        Port = 9092
    });

    bool quiet = args.Contains("--quiet") || args.Contains("-q");
    string? scriptPath = ArgValue(args, "--script");

    Diag.Enabled = true;
    Diag.MinLevel = DiagLevel.Info;
    Diag.Emitted += (level, message, category) =>
    {
        string tag = category is null ? "" : category + " ";
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level} {tag}{message}");
    };

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    string scripts_directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QX Scripter",
        "scripts");
    await using var runtime = new RuntimeHost(new RuntimeHostOptions
    {
        GEarth = options,
        ScriptsDirectory = scripts_directory
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

    if (scriptPath is not null)
    {
        extension.Connected += session =>
        {
            _ = RunScriptAsync(
                runtime.ScriptExecution,
                scriptPath,
                cts.Token);
        };
    }

    Console.WriteLine($"QX {options.Version} - G-Earth extension on port {options.Port}. Ctrl+C to quit. (-q disables packet log)");

    try
    {
        await runtime.StartAsync(cts.Token);
        await runtime.TransportTask.WaitAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("Fatal: " + ex.Message);
        return 1;
    }
    return 0;
}

static async Task RunScriptAsync(
    ScriptExecutionService script_execution,
    string scriptPath,
    CancellationToken cancellationToken)
{
    try
    {
        string path = Path.GetFullPath(scriptPath);
        string code = await File.ReadAllTextAsync(path, cancellationToken);
        Diag.Info($"Running script {Path.GetFileName(path)}", "script");
        ScriptExecutionResult result = await script_execution.RunAsync(new ScriptExecutionRequest
        {
            Code = code,
            SourceIdentity = path,
            FileName = path,
            OutputWritten = message => Diag.Info(message, "script")
        }, cancellationToken);

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
