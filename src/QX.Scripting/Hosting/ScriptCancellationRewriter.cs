using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace Qx.Scripting.Hosting;

/// <summary>The rewritten script and the rewritten text of every file it loads.</summary>
/// <param name="Main">The script's own code.</param>
/// <param name="Loaded">Each <c>#load</c>ed file's code, keyed by its resolved path.</param>
internal sealed record ScriptRewrite(string Main, IReadOnlyDictionary<string, string> Loaded);

internal sealed class ScriptCancellationRewriter(SemanticModel semanticModel) : CSharpSyntaxRewriter
{
    private static readonly string context_type = $"global::{typeof(ScriptExecutionContext).FullName}";

    public static ScriptRewrite Rewrite(Script<object> script)
    {
        Compilation compilation = script.GetCompilation();
        SyntaxTree main = compilation.SyntaxTrees.FirstOrDefault(tree =>
                string.Equals(tree.FilePath, script.Options.FilePath, StringComparison.OrdinalIgnoreCase))
            ?? compilation.SyntaxTrees.Last();
        var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (!ReferenceEquals(tree, main))
                loaded[tree.FilePath] = Rewrite(compilation, tree);
        }
        return new ScriptRewrite(Rewrite(compilation, main), loaded);
    }

    private static string Rewrite(Compilation compilation, SyntaxTree tree) =>
        new ScriptCancellationRewriter(compilation.GetSemanticModel(tree, true)).Visit(tree.GetRoot())!.ToFullString();

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var rewritten = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return rewritten.WithStatement(WithCancellationCheck(rewritten.Statement));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var rewritten = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return rewritten.WithStatement(WithCancellationCheck(rewritten.Statement));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var rewritten = (ForStatementSyntax)base.VisitForStatement(node)!;
        return rewritten.WithStatement(WithCancellationCheck(rewritten.Statement));
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var rewritten = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return rewritten.WithStatement(WithCancellationCheck(rewritten.Statement));
    }

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var rewritten = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return rewritten.WithStatement(WithCancellationCheck(rewritten.Statement));
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        IMethodSymbol? method = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

        if (IsMethod(method, "System.Threading.Tasks.Task", "Delay"))
            return WithRuntimeMethod(rewritten, nameof(ScriptExecutionContext.Delay));

        if (IsMethod(method, "System.Threading.Thread", "Sleep"))
            return WithRuntimeMethod(rewritten, nameof(ScriptExecutionContext.Sleep));

        return rewritten;
    }

    private static StatementSyntax WithCancellationCheck(StatementSyntax statement)
    {
        if (!CanBeEmbedded(statement))
            return statement;

        StatementSyntax check = SyntaxFactory.ParseStatement(
            $"{context_type}.{nameof(ScriptExecutionContext.ThrowIfCancellationRequested)}();");
        if (statement is BlockSyntax block)
            return block.WithStatements(block.Statements.Insert(0, check));

        SyntaxTriviaList leadingTrivia = statement.GetLeadingTrivia();
        return SyntaxFactory
            .Block(check, statement.WithoutLeadingTrivia())
            .WithLeadingTrivia(leadingTrivia);
    }

    private static bool CanBeEmbedded(StatementSyntax statement) =>
        statement is not (LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax);

    private static InvocationExpressionSyntax WithRuntimeMethod(
        InvocationExpressionSyntax invocation,
        string method)
    {
        ExpressionSyntax expression = SyntaxFactory.ParseExpression($"{context_type}.{method}");
        return invocation.WithExpression(expression.WithTriviaFrom(invocation.Expression));
    }

    private static bool IsMethod(IMethodSymbol? method, string containingType, string name) =>
        method?.Name == name &&
        method.ContainingType.ToDisplayString() == containingType;
}
