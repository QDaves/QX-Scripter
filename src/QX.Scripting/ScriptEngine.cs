using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Qx.Game;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;

namespace Qx.Scripting;

public static class ScriptEngine
{
    private static ScriptOptions? _options;

    public static IReadOnlyList<Assembly> ReferenceAssemblies { get; } =
    [
        typeof(ScriptGlobals).Assembly,
        typeof(IInterceptor).Assembly,
        typeof(RoomManager).Assembly,
        typeof(IPacket).Assembly,
        typeof(RoomEntryInfo).Assembly
    ];

    public static IReadOnlyList<string> Imports { get; } =
    [
        "System",
        "System.Collections.Generic",
        "System.Collections.Concurrent",
        "System.Globalization",
        "System.Linq",
        "System.Text",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Tasks",
        "Qx",
        "Qx.Messages",
        "Qx.Interception",
        "Qx.Game",
        "Qx.Model",
        "Qx.Model.Bots",
        "Qx.Model.Crafting",
        "Qx.Model.Figures",
        "Qx.Model.Forums",
        "Qx.Model.Marketplace",
        "Qx.Model.Messages.Incoming",
        "Qx.Model.Messages.Outgoing",
        "Qx.Model.Polls",
        "Qx.Model.Quests",
        "Qx.Model.Subscriptions",
        "Qx.Model.Wired",
        "Qx.Game.Snapshots",
        "Qx.Scripting"
    ];

    private static ScriptOptions Options => _options ??= Build();

    private static ScriptOptions Build() =>
        ScriptOptions.Default
            .WithReferences(ReferenceAssemblies.Where(HasPhysicalMetadata))
            .WithImports(Imports)
            .WithEmitDebugInformation(true)
            .WithOptimizationLevel(OptimizationLevel.Debug);

    [UnconditionalSuppressMessage(
        "SingleFile",
        "IL3002",
        Justification = "Scripting references are admitted only when the host extracted managed assemblies.")]
    private static bool HasPhysicalMetadata(Assembly assembly) =>
        File.Exists(assembly.ManifestModule.FullyQualifiedName);

    public static ScriptProgram Prepare(string code, string fileName = "script.csx")
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        Script<object> script = CSharpScript.Create(
            code,
            Options.WithFilePath(fileName).WithFileEncoding(Encoding.UTF8),
            typeof(ScriptGlobals));
        string rewritten = ScriptCancellationRewriter.Rewrite(script);
        if (!string.Equals(code, rewritten, StringComparison.Ordinal))
        {
            script = CSharpScript.Create(
                rewritten,
                Options.WithFilePath(fileName).WithFileEncoding(Encoding.UTF8),
                typeof(ScriptGlobals));
        }
        return new ScriptProgram(script);
    }

    public static Task RunAsync(
        string code,
        ScriptGlobals globals,
        CancellationToken cancellationToken = default) =>
        Prepare(code).RunAsync(globals, cancellationToken);

    public static Task RunAsync(
        string code,
        ScriptGlobals globals,
        string fileName,
        CancellationToken cancellationToken = default) =>
        Prepare(code, fileName).RunAsync(globals, cancellationToken);

    public static ImmutableArray<Diagnostic> Compile(string code, string fileName = "script.csx") =>
        Prepare(code, fileName).Diagnostics;
}

public sealed class ScriptProgram
{
    private readonly ScriptRunner<object>? _runner;

    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public bool HasErrors => Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    internal ScriptProgram(Script<object> script)
    {
        Diagnostics = script.Compile();
        if (!HasErrors)
            _runner = script.CreateDelegate();
    }

    public async Task RunAsync(ScriptGlobals globals, CancellationToken cancellationToken = default)
    {
        if (_runner is null)
            throw new CompilationErrorException("Script compilation failed.", Diagnostics);
        CancellationToken scriptCancellation = cancellationToken.CanBeCanceled
            ? cancellationToken
            : globals.BaseCancellationToken;
        using IDisposable scope = ScriptExecutionContext.Enter(scriptCancellation);
        _ = await _runner(globals, scriptCancellation).ConfigureAwait(false);
        scriptCancellation.ThrowIfCancellationRequested();
    }
}
