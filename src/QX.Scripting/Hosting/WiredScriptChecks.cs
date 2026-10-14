using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qx.Model.Wired;

namespace Qx.Scripting.Hosting;

[SuppressMessage("MicrosoftCodeAnalysisReleaseTracking", "RS2008", Justification = "Diagnostics run inside the script host, not an analyzer package.")]
internal static class WiredScriptChecks
{
    internal static readonly DiagnosticDescriptor invalid_value = new("QX1101", "Invalid Wired setting", "{0}",
        "QX", DiagnosticSeverity.Warning, true);
    internal static readonly DiagnosticDescriptor incomplete_area = new("QX1102", "Incomplete Wired area", "{0}",
        "QX", DiagnosticSeverity.Warning, true);

    private static readonly IReadOnlyDictionary<string, WiredFormDefinition> forms = WiredFormRegistry.Definitions
        .ToDictionary(form => "Qx.Model.Wired." + form.SettingsType, StringComparer.Ordinal);

    internal static ImmutableArray<DiagnosticDescriptor> Diagnostics => [invalid_value, incomplete_area];

    internal static void Register(CompilationStartAnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(assignment, SyntaxKind.SimpleAssignmentExpression);
        context.RegisterSyntaxNodeAction(invocation, SyntaxKind.InvocationExpression);
    }

    private static void assignment(SyntaxNodeAnalysisContext context)
    {
        var expression = (AssignmentExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(expression.Left, context.CancellationToken).Symbol is not IPropertySymbol property ||
            !forms.TryGetValue(property.ContainingType.ToDisplayString(), out WiredFormDefinition? form)) return;
        WiredFormFieldDefinition? field = form.Fields.FirstOrDefault(field => property.Name == property_name(field.Name));
        Optional<object?> value = context.SemanticModel.GetConstantValue(expression.Right, context.CancellationToken);
        if (field is null || !value.HasValue || value.Value is null) return;
        string? problem = null;
        if (value.Value is string text && field.MaximumLength is { } length && text.Length > length)
            problem = $"{property.Name} accepts at most {length} characters.";
        if (value.Value is int number)
        {
            if (field.Minimum is { } minimum && number < minimum || field.Maximum is { } maximum && number > maximum)
                problem = $"{property.Name}: {field.Values}.";
            IReadOnlyList<WiredFormChoice> choices = WiredFormChoices.Get(form, field.Name);
            if (choices.Count > 0 && !WiredFormChoices.IsFlags(form, field.Name) && !choices.Any(choice => choice.Value == number))
                problem = $"{property.Name}: use {string.Join(", ", choices.Select(choice => choice.Name))}.";
        }
        if (problem is not null) context.ReportDiagnostic(Diagnostic.Create(invalid_value, expression.Right.GetLocation(), problem));
    }

    private static void invocation(SyntaxNodeAnalysisContext context)
    {
        var expression = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol is not IMethodSymbol method ||
            method.Name != "Add" || method.ContainingType.ToDisplayString() != "Qx.Game.Application.WiredStackPlan" ||
            method.TypeArguments.Length != 1 || !forms.TryGetValue(method.TypeArguments[0].ToDisplayString(), out WiredFormDefinition? form) ||
            form.Category != WiredFormCategory.Selector || !form.Fields.Any(field => field.Name == "width")) return;
        LambdaExpressionSyntax? lambda = expression.ArgumentList.Arguments.Select(argument => argument.Expression).OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambda is null) return;
        if (lambda.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
            context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol helper &&
            helper.Name == "Area" && helper.ContainingType.ToDisplayString() == "Qx.Game.Application.WiredSettings")) return;
        string[] properties = lambda.DescendantNodes().OfType<AssignmentExpressionSyntax>().Select(assignment =>
            context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol).OfType<IPropertySymbol>()
            .Select(property => property.Name).ToArray();
        string[] missing = new[] { "X", "Y", "Width", "Height" }.Except(properties, StringComparer.Ordinal).ToArray();
        if (missing.Length > 0) context.ReportDiagnostic(Diagnostic.Create(incomplete_area, lambda.GetLocation(),
            $"Set {string.Join(", ", missing)} for a new area selector, or use WiredSettings.Area(form, x, y, width, height)."));
    }

    private static string property_name(string name) => string.Concat(name.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
