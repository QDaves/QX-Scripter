using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Qx.Scripting.Hosting;

[SuppressMessage(
    "MicrosoftCodeAnalysisCorrectness",
    "RS1001",
    Justification = "ScriptEngine creates the analyzer itself; no compiler or IDE discovers it by attribute.")]
[SuppressMessage(
    "MicrosoftCodeAnalysisReleaseTracking",
    "RS2008",
    Justification = "The analyzer runs inside QX and is never shipped as an analyzer package.")]
internal sealed class ScriptAnalyzer(MessageNames names) : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor unknown_message = new(
        "QX1001",
        "Unknown message name",
        "{0}",
        "QX",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The name is neither in the message registry nor in the bound client catalog for the call's direction, so a handler never runs, a wait times out and a send throws.");

    private static readonly DiagnosticDescriptor model_mismatch = new(
        "QX1002",
        "Message parsed as a different model",
        "{0}",
        "QX",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The message's contract parses it into another QX model, so every matching packet fails to parse.");

    private static readonly DiagnosticDescriptor unknown_directive = new(
        "QX1004",
        "Unknown panel directive",
        "{0}",
        "QX",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The panel ignores a //@ui directive it does not know.");

    private static readonly DiagnosticDescriptor undeclared_control = new(
        "QX1005",
        "Undeclared panel control",
        "{0}",
        "QX",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The panel declares no control with the name and the script never stores it with Ui.Set, so reads return their fallback and the panel ignores writes.");

    private static readonly FrozenDictionary<string, MessageDirection> message_methods = new Dictionary<string, MessageDirection>
    {
        [nameof(ScriptGlobals.OnIn)] = MessageDirection.In,
        [nameof(ScriptGlobals.SendToClient)] = MessageDirection.In,
        [nameof(ScriptGlobals.OnOut)] = MessageDirection.Out,
        [nameof(ScriptGlobals.SendToServer)] = MessageDirection.Out,
        [nameof(ScriptGlobals.Receive)] = MessageDirection.Both,
        [nameof(ScriptGlobals.ReceiveAsync)] = MessageDirection.Both,
        [nameof(ScriptGlobals.ReceiveAnyAsync)] = MessageDirection.Both,
        [nameof(ScriptGlobals.TryReceive)] = MessageDirection.Both
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> control_methods = new[]
    {
        nameof(ScriptUi.String),
        nameof(ScriptUi.Text),
        nameof(ScriptUi.Select),
        nameof(ScriptUi.Int),
        nameof(ScriptUi.Number),
        nameof(ScriptUi.Bool),
        nameof(ScriptUi.File),
        nameof(ScriptUi.FileText),
        nameof(ScriptUi.Set),
        nameof(ScriptUi.Log),
        nameof(ScriptUi.Clear),
        nameof(ScriptUi.Progress),
        nameof(ScriptUi.Status),
        nameof(ScriptUi.Enable),
        nameof(ScriptUi.Show),
        nameof(ScriptUi.AddRow),
        nameof(ScriptUi.SetRows),
        nameof(ScriptUi.Busy),
        nameof(ScriptUi.OnClick),
        nameof(ScriptUi.OnChange),
        nameof(ScriptUi.Clicked)
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly string directive_names = string.Join(", ",
        UiSpec.Directives.SelectMany(directive => directive.Aliases.Prepend(directive.Key)).Order(StringComparer.Ordinal));

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [unknown_message, model_mismatch, unknown_directive, undeclared_control];

    public static ImmutableArray<Diagnostic> Analyze(Compilation compilation, MessageNames names) =>
        compilation.WithAnalyzers([new ScriptAnalyzer(names)])
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(Start);
    }

    private void Start(CompilationStartAnalysisContext context)
    {
        Compilation compilation = context.Compilation;
        if (compilation.GetTypeByMetadataName(typeof(ScriptGlobals).FullName!) is not { } globals ||
            compilation.GetTypeByMetadataName(typeof(ScriptUi).FullName!) is not { } ui ||
            compilation.GetTypeByMetadataName(typeof(HeaderIndex).FullName!) is not { } header_index)
        {
            return;
        }

        var checks = new ScriptChecks(names, compilation, globals, ui, header_index);
        context.RegisterSyntaxTreeAction(checks.CheckDirectives);
        context.RegisterSemanticModelAction(checks.CheckCalls);
    }

    private sealed class ScriptChecks(
        MessageNames names,
        Compilation compilation,
        INamedTypeSymbol globals,
        INamedTypeSymbol ui,
        INamedTypeSymbol header_index)
    {
        private readonly ConcurrentDictionary<SyntaxTree, UiSpec> _panels = new();
        private readonly ConcurrentDictionary<Type, INamedTypeSymbol?> _models = new();

        public void CheckDirectives(SyntaxTreeAnalysisContext context)
        {
            foreach (UiUnknownDirective directive in Panel(context.Tree, context.CancellationToken).UnknownDirectives)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    unknown_directive,
                    Location.Create(context.Tree, new TextSpan(directive.Offset, directive.Key.Length)),
                    $"'//@ui:{directive.Key}' is not a panel directive. The directives are {directive_names}."));
            }
        }

        public void CheckCalls(SemanticModelAnalysisContext context)
        {
            var controls = new List<ControlCall>();
            foreach (SyntaxNode node in context.SemanticModel.SyntaxTree.GetRoot(context.CancellationToken).DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        CheckInvocation(context, invocation, controls);
                        break;
                    case ElementAccessExpressionSyntax access:
                        CheckHeaderLookup(context, access);
                        break;
                }
            }
            CheckControls(context, controls);
        }

        private void CheckInvocation(
            SemanticModelAnalysisContext context,
            InvocationExpressionSyntax invocation,
            List<ControlCall> controls)
        {
            if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
                return;
            if (SymbolEqualityComparer.Default.Equals(method.ContainingType, globals) &&
                message_methods.TryGetValue(method.Name, out MessageDirection direction))
            {
                CheckMessages(context, invocation.ArgumentList, method, direction);
            }
            else if (SymbolEqualityComparer.Default.Equals(method.ContainingType, ui) &&
                     control_methods.Contains(method.Name) &&
                     Arguments(invocation.ArgumentList, method.Parameters)
                         .Where(argument => argument.Parameter.Ordinal == 0)
                         .Select(argument => argument.Value)
                         .FirstOrDefault() is { } value &&
                     Constant(context, value) is { } control)
            {
                controls.Add(new ControlCall(method.Name, value, control));
            }
        }

        private void CheckHeaderLookup(SemanticModelAnalysisContext context, ElementAccessExpressionSyntax access)
        {
            SemanticModel model = context.SemanticModel;
            if (model.GetSymbolInfo(access, context.CancellationToken).Symbol is not IPropertySymbol { IsIndexer: true } indexer ||
                !SymbolEqualityComparer.Default.Equals(indexer.ContainingType, header_index) ||
                model.GetSymbolInfo(access.Expression, context.CancellationToken).Symbol is not IPropertySymbol source ||
                !SymbolEqualityComparer.Default.Equals(source.ContainingType, globals) ||
                source.Name is not (nameof(ScriptGlobals.In) or nameof(ScriptGlobals.Out)))
            {
                return;
            }

            MessageDirection direction = source.Name == nameof(ScriptGlobals.In) ? MessageDirection.In : MessageDirection.Out;
            foreach (ArgumentSyntax argument in access.ArgumentList.Arguments)
            {
                if (Constant(context, argument.Expression) is { } name && !names.IsKnown(direction, name))
                    Report(context, unknown_message, argument.Expression, names.Unknown(direction, name));
            }
        }

        private void CheckMessages(
            SemanticModelAnalysisContext context,
            ArgumentListSyntax arguments,
            IMethodSymbol method,
            MessageDirection direction)
        {
            INamedTypeSymbol? model = method.IsGenericMethod &&
                method.TypeArguments[0] is INamedTypeSymbol argument &&
                MessageNames.IsModelAssembly(argument.ContainingAssembly?.Name)
                    ? argument
                    : null;

            foreach (ExpressionSyntax value in Arguments(arguments, method.Parameters).SelectMany(NameValues))
            {
                if (Constant(context, value) is not { } name)
                    continue;
                if (!names.IsKnown(direction, name))
                    Report(context, unknown_message, value, names.Unknown(direction, name));
                else if (model is not null)
                    CheckModel(context, value, direction, name, model);
            }
        }

        private void CheckModel(
            SemanticModelAnalysisContext context,
            ExpressionSyntax value,
            MessageDirection direction,
            string name,
            INamedTypeSymbol model)
        {
            bool IsModel(Type type) => SymbolEqualityComparer.Default.Equals(ModelSymbol(type), model);
            if (names.ModelDirections(direction, name, IsModel) == MessageDirection.None)
                Report(context, model_mismatch, value, names.Mismatch(direction, name, model.Name, IsModel));
        }

        private void CheckControls(SemanticModelAnalysisContext context, List<ControlCall> controls)
        {
            if (controls.Count == 0)
                return;
            UiSpec panel = Panel(context.SemanticModel.SyntaxTree, context.CancellationToken);
            if (!panel.HasUi)
                return;

            var declared = new HashSet<string>(panel.Controls, StringComparer.OrdinalIgnoreCase);
            declared.UnionWith(controls.Where(control => control.Method == nameof(ScriptUi.Set)).Select(control => control.Name));
            foreach (ControlCall control in controls)
            {
                if (!declared.Contains(control.Name))
                    Report(context, undeclared_control, control.Value, $"The panel declares no control named '{control.Name}'.");
            }
        }

        private UiSpec Panel(SyntaxTree tree, CancellationToken cancellation_token) =>
            _panels.GetOrAdd(tree, key => UiSpec.Parse(key.GetText(cancellation_token).ToString()));

        private INamedTypeSymbol? ModelSymbol(Type type) =>
            _models.GetOrAdd(type, key => compilation.GetTypeByMetadataName(key.FullName!));

        private static IEnumerable<Argument> Arguments(BaseArgumentListSyntax list, ImmutableArray<IParameterSymbol> parameters)
        {
            for (int index = 0; index < list.Arguments.Count; index++)
            {
                ArgumentSyntax argument = list.Arguments[index];
                IParameterSymbol? parameter = argument.NameColon is { } name
                    ? parameters.FirstOrDefault(candidate => candidate.Name == name.Name.Identifier.ValueText)
                    : index < parameters.Length
                        ? parameters[index]
                        : parameters.LastOrDefault(candidate => candidate.IsParams);
                if (parameter is not null)
                    yield return new Argument(parameter, argument.Expression);
            }
        }

        private static IEnumerable<ExpressionSyntax> NameValues(Argument argument) =>
            argument.Parameter.Type switch
            {
                { SpecialType: SpecialType.System_String } => [argument.Value],
                IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_String } => argument.Value switch
                {
                    ImplicitArrayCreationExpressionSyntax creation => creation.Initializer.Expressions,
                    ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
                    CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(element => element.Expression),
                    _ => [argument.Value]
                },
                _ => []
            };

        private static string? Constant(SemanticModelAnalysisContext context, ExpressionSyntax value) =>
            context.SemanticModel.GetConstantValue(value, context.CancellationToken) is { HasValue: true, Value: string text } &&
            !string.IsNullOrWhiteSpace(text)
                ? text
                : null;

        private static void Report(SemanticModelAnalysisContext context, DiagnosticDescriptor descriptor, ExpressionSyntax value, string message) =>
            context.ReportDiagnostic(Diagnostic.Create(descriptor, value.GetLocation(), message));

        private readonly record struct Argument(IParameterSymbol Parameter, ExpressionSyntax Value);

        private readonly record struct ControlCall(string Method, ExpressionSyntax Value, string Name);
    }
}
