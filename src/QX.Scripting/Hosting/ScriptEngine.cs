using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Scripting;
using Qx.Game;
using Qx.Interception;
using Qx.Messages;
using Qx.Model.Messages.Incoming;
using Qx.Protocol;

namespace Qx.Scripting.Hosting;

/// <summary>
/// Provides compilation and execution of QX scripts against <see cref="ScriptGlobals"/>.
/// </summary>
public static class ScriptEngine
{
    private static ScriptOptions? _options;

    /// <summary>Gets the assemblies that scripts are compiled against.</summary>
    /// <remarks>
    /// An assembly without a physical file on disk, such as one bundled into a single-file host
    /// that was not extracted, is not passed to the compiler.
    /// </remarks>
    public static IReadOnlyList<Assembly> ReferenceAssemblies { get; } =
    [
        typeof(ScriptGlobals).Assembly,
        typeof(IInterceptor).Assembly,
        typeof(RoomManager).Assembly,
        typeof(IPacket).Assembly,
        typeof(MessageKey).Assembly,
        typeof(RoomEntryInfo).Assembly,
        typeof(Qx.Platform.KeyboardReader).Assembly,
        typeof(StoragePaths).Assembly
    ];

    /// <summary>Gets the namespaces every script imports without a using directive.</summary>
    public static IReadOnlyList<string> Imports { get; } =
    [
        "System",
        "System.Collections.Generic",
        "System.Collections.Concurrent",
        "System.Diagnostics",
        "System.Globalization",
        "System.IO",
        "System.Linq",
        "System.Text",
        "System.Text.RegularExpressions",
        "System.Threading",
        "System.Threading.Tasks",
        "Qx",
        "Qx.Messages",
        "Qx.Protocol",
        "Qx.Interception",
        "Qx.Game",
        "Qx.Game.Application",
        "Qx.Model",
        "Qx.Model.Figures",
        "Qx.Model.Messages.Incoming",
        "Qx.Model.Messages.Outgoing",
        "Qx.Model.Wired",
        "Qx.Platform",
        "Qx.Scripting"
    ];

    /// <summary>Gets the IDs of the compiler diagnostics that are never reported for a script.</summary>
    /// <remarks>
    /// CS8632 and its generated-code twin CS8669 are among them, so a nullable annotation such as
    /// <c>User?</c> compiles without a warning although scripts have no nullable context. A script
    /// that writes <c>#nullable enable</c> still gets the null warnings.
    /// </remarks>
    public static IReadOnlyList<string> SuppressedDiagnostics { get; } = ["CS8632", "CS8669"];

    internal static ScriptOptions Options => _options ??= Build();

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

    /// <summary>
    /// Compiles a script, making every loop in it and in the files it loads stop with the run.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every <c>while</c>, <c>do</c>, <c>for</c> and <c>foreach</c> body starts with a
    /// cancellation check, and calls to every overload of <c>Task.Delay</c> and
    /// <c>Thread.Sleep</c> are redirected to <see cref="ScriptExecutionContext"/> so they end
    /// when the run is stopped. Compile errors are reported through
    /// <see cref="ScriptProgram.Diagnostics"/> rather than thrown.
    /// </para>
    /// <para>
    /// The source as written also goes through the analyzer of <see cref="CreateAnalyzer"/>, so the
    /// diagnostics include its findings. They point into the source as written whenever it reports
    /// them, and are chosen and ordered as <see cref="ScriptProgram.Diagnostics"/> describes.
    /// </para>
    /// </remarks>
    /// <param name="code">The script source code.</param>
    /// <param name="fileName">The script path, or a name for a script that has no file.</param>
    /// <param name="directory">
    /// The folder <c>#load</c> and <c>#r</c> paths resolve against when the script's own path
    /// does not settle them, normally the script library.
    /// </param>
    /// <param name="messages">
    /// The message resolver whose registry and bound catalog decide which message names are known,
    /// or <see langword="null"/> to know only the names of the embedded registry.
    /// </param>
    /// <returns>The compiled program, which carries the compiler and analyzer diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="code"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/>, empty or whitespace.</exception>
    public static ScriptProgram Prepare(
        string code,
        string fileName = "script.csx",
        string? directory = null,
        IMessageResolver? messages = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ScriptOptions options = Options
            .WithFilePath(fileName)
            .WithFileEncoding(Encoding.UTF8)
            .WithSourceResolver(new ScriptSourceResolver(directory))
            .WithMetadataResolver(ScriptMetadataResolver.Default.WithBaseDirectory(directory));
        Script<object> source = CSharpScript.Create(code, options, typeof(ScriptGlobals));
        ImmutableArray<Diagnostic> findings = ScriptAnalyzer.Analyze(source.GetCompilation(), MessageNames.For(messages));
        ScriptRewrite rewrite = ScriptCancellationRewriter.Rewrite(source);
        Script<object> script = source;
        if (rewrite.Loaded.Count > 0 || !string.Equals(code, rewrite.Main, StringComparison.Ordinal))
        {
            script = CSharpScript.Create(
                rewrite.Main,
                options.WithSourceResolver(new ScriptSourceResolver(directory, rewrite.Loaded)),
                typeof(ScriptGlobals));
        }
        return new ScriptProgram(script, source, findings);
    }

    /// <summary>
    /// Creates the analyzer that checks the message names, message models and panel directives of a
    /// script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It checks constant arguments only and reports:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// QX1001, a warning, for a message name that is neither in the registry nor in the bound
    /// catalog for the call's direction, with the closest known name or the direction it exists in.
    /// It is not an error because the catalog of a newer client build can add names.
    /// </description></item>
    /// <item><description>
    /// QX1002, an error, when <c>OnIn&lt;T&gt;</c>, <c>OnOut&lt;T&gt;</c> or
    /// <c>ReceiveAsync&lt;T&gt;</c> names a message whose contract parses it into another QX model.
    /// Models a script defines itself and messages without a contract are never reported.
    /// </description></item>
    /// <item><description>QX1004, a warning, for a <c>//@ui:</c> directive the panel does not know.</description></item>
    /// <item><description>
    /// QX1005, a warning, for a <see cref="ScriptUi"/> call on a control the script's panel does
    /// not declare. A name the script stores with <see cref="ScriptUi.Set"/> counts as declared.
    /// </description></item>
    /// </list>
    /// <para>
    /// <see cref="Prepare"/> runs it on every script; an editor adds it to its documents so it
    /// shows the same findings.
    /// </para>
    /// </remarks>
    /// <param name="messages">
    /// The message resolver whose registry and bound catalog decide which message names are known,
    /// or <see langword="null"/> to know only the names of the embedded registry.
    /// </param>
    /// <returns>A new analyzer.</returns>
    public static DiagnosticAnalyzer CreateAnalyzer(IMessageResolver? messages = null) =>
        new ScriptAnalyzer(MessageNames.For(messages));

    /// <summary>
    /// Compiles and runs a script under the file name <c>script.csx</c>.
    /// </summary>
    /// <param name="code">The script source code.</param>
    /// <param name="globals">The globals the script runs against.</param>
    /// <param name="cancellationToken">
    /// The token that stops the script, or <see langword="default"/> to use the token the globals
    /// were created with.
    /// </param>
    /// <returns>A task that completes when the script body has finished.</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script has compile errors.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped.</exception>
    public static Task RunAsync(
        string code,
        ScriptGlobals globals,
        CancellationToken cancellationToken = default) =>
        Prepare(code).RunAsync(globals, cancellationToken);

    /// <summary>
    /// Compiles and runs a script under the specified file name.
    /// </summary>
    /// <param name="code">The script source code.</param>
    /// <param name="globals">The globals the script runs against.</param>
    /// <param name="fileName">The script path, or a name for a script that has no file.</param>
    /// <param name="cancellationToken">
    /// The token that stops the script, or <see langword="default"/> to use the token the globals
    /// were created with.
    /// </param>
    /// <returns>A task that completes when the script body has finished.</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script has compile errors.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the script was stopped.</exception>
    public static Task RunAsync(
        string code,
        ScriptGlobals globals,
        string fileName,
        CancellationToken cancellationToken = default) =>
        Prepare(code, fileName).RunAsync(globals, cancellationToken);

    /// <summary>
    /// Compiles a script without running it.
    /// </summary>
    /// <param name="code">The script source code.</param>
    /// <param name="fileName">The script path, or a name for a script that has no file.</param>
    /// <param name="directory">
    /// The folder <c>#load</c> and <c>#r</c> paths resolve against when the script's own path
    /// does not settle them, or <see langword="null"/>.
    /// </param>
    /// <param name="messages">
    /// The message resolver whose registry and bound catalog decide which message names are known,
    /// or <see langword="null"/> to know only the names of the embedded registry.
    /// </param>
    /// <returns>
    /// The compiler and analyzer diagnostics, including warnings but without
    /// <see cref="SuppressedDiagnostics"/>, ordered and placed as <see cref="ScriptProgram.Diagnostics"/>
    /// describes; empty when the script compiles cleanly.
    /// </returns>
    public static ImmutableArray<Diagnostic> Compile(
        string code,
        string fileName = "script.csx",
        string? directory = null,
        IMessageResolver? messages = null) =>
        Prepare(code, fileName, directory, messages).Diagnostics;

    /// <summary>
    /// Gets the using directive or the namespace that a diagnostic about a missing type or name
    /// points to.
    /// </summary>
    /// <remarks>
    /// The name is looked up among the public top-level types of the assemblies scripts compile
    /// against. CS0246, and CS0103 on a name a member is accessed on, get the using directives
    /// that bring a type of that name into scope, without the namespaces of <see cref="Imports"/>.
    /// CS0234, a type looked up in a namespace that does not declare it, gets every namespace that
    /// does.
    /// </remarks>
    /// <param name="diagnostic">A diagnostic of <see cref="Compile"/> or <see cref="ScriptProgram.Diagnostics"/>.</param>
    /// <returns>
    /// A hint such as <c>add using System.Text.Json;</c> or <c>'SessionRules' is in Qx.Game.Rules</c>,
    /// or <see langword="null"/> when the diagnostic is of another kind or there is no namespace to name.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="diagnostic"/> is <see langword="null"/>.</exception>
    public static string? UsingHint(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return UsingHints.For(diagnostic);
    }

    /// <summary>
    /// Compiles, without running, a small script that uses the everyday constructs, so the first
    /// real run does not pay for loading and JIT compiling the compiler.
    /// </summary>
    /// <remarks>
    /// Call it off the UI thread. The sample has a loop, so it also compiles the cancellation check
    /// that <see cref="Prepare"/> inserts into every loop body.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sample, or the cancellation check inserted into it, no longer compiles against
    /// the API.
    /// </exception>
    public static void WarmUp()
    {
        ScriptProgram program = Prepare(
            """
            var items = FloorItems.Where(item => item.Id > 0).ToList();
            for (int index = 0; index < items.Count; index++)
                await Delay(0);
            Log(items.Count);
            """,
            "warm-up.csx");
        if (program.Diagnostics.FirstOrDefault(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) is { } error)
            throw new InvalidOperationException($"The compiler warm-up script no longer compiles: {error}");
    }
}

/// <summary>
/// Represents a compiled script, created by <see cref="ScriptEngine.Prepare(string, string, string?, IMessageResolver?)"/>.
/// </summary>
public sealed class ScriptProgram
{
    private readonly ScriptRunner<object>? _runner;

    /// <summary>
    /// Gets the diagnostics the compiler and the script analyzer reported, including warnings but
    /// without <see cref="ScriptEngine.SuppressedDiagnostics"/>.
    /// </summary>
    /// <remarks>
    /// They are ordered by position, the script's own file first and then each loaded file by path.
    /// The program runs with the cancellation checks that
    /// <see cref="ScriptEngine.Prepare(string, string, string?, IMessageResolver?)"/> adds. When it
    /// reports diagnostics, those of the source as written take their place if the source as
    /// written has errors, or if neither has errors and the source as written has warnings.
    /// Otherwise those of the program with the checks are kept and point into the rewritten text,
    /// so an error in the checks still blocks the run.
    /// </remarks>
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    /// <summary>Gets whether any diagnostic is an error, in which case the program cannot run.</summary>
    public bool HasErrors => Diagnostics.Any(IsError);

    internal ScriptProgram(Script<object> script, Script<object> source, ImmutableArray<Diagnostic> findings)
    {
        string file_path = source.Options.FilePath;
        Diagnostics =
        [
            .. Compile(script, source)
                .Concat(findings)
                .Where(diagnostic => !ScriptEngine.SuppressedDiagnostics.Contains(diagnostic.Id))
                .OrderBy(diagnostic => FileOrder(diagnostic, file_path))
                .ThenBy(diagnostic => diagnostic.Location.SourceTree?.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(diagnostic => diagnostic.Location.SourceSpan.Start)
        ];
        if (!HasErrors)
            _runner = script.CreateDelegate();
    }

    /// <summary>
    /// Runs the compiled script against the specified globals.
    /// </summary>
    /// <remarks>
    /// The token becomes the ambient script token for the run, which <see cref="ScriptGlobals.Ct"/>,
    /// the loop checks and the redirected delays inside the script observe.
    /// </remarks>
    /// <param name="globals">The globals the script runs against.</param>
    /// <param name="cancellationToken">
    /// The token that stops the script, or <see langword="default"/> to use the token the globals
    /// were created with.
    /// </param>
    /// <returns>A task that completes when the script body has finished.</returns>
    /// <exception cref="CompilationErrorException">Thrown when the script has compile errors.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the token was canceled while the script ran or by the time its body returned.
    /// </exception>
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

    private static ImmutableArray<Diagnostic> Compile(Script<object> script, Script<object> source)
    {
        ImmutableArray<Diagnostic> diagnostics = script.Compile();
        if (ReferenceEquals(script, source) || diagnostics.IsEmpty)
            return diagnostics;
        ImmutableArray<Diagnostic> written = Check(source.GetCompilation());
        return written.Any(IsError) || (!written.IsEmpty && !diagnostics.Any(IsError)) ? written : diagnostics;
    }

    private static ImmutableArray<Diagnostic> Check(Compilation compilation)
    {
        ImmutableArray<Diagnostic> parse_errors = [.. compilation.GetParseDiagnostics().Where(IsError)];
        if (!parse_errors.IsEmpty)
            return parse_errors;
        ImmutableArray<Diagnostic> diagnostics = compilation.GetDiagnostics();
        DiagnosticSeverity severity = diagnostics.Any(IsError) ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning;
        return [.. diagnostics.Where(diagnostic => diagnostic.Severity == severity)];
    }

    private static int FileOrder(Diagnostic diagnostic, string file_path) =>
        diagnostic.Location.SourceTree?.FilePath switch
        {
            null => 2,
            string path when string.Equals(path, file_path, StringComparison.OrdinalIgnoreCase) => 0,
            _ => 1
        };

    private static bool IsError(Diagnostic diagnostic) => diagnostic.Severity == DiagnosticSeverity.Error;
}
