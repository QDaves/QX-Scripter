using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace Qx.Scripting;

internal sealed class ScriptCancellationRewriter(SemanticModel semanticModel) : CSharpSyntaxRewriter
{
    public static string Rewrite(Script<object> script)
    {
        Compilation compilation = script.GetCompilation();
        SyntaxTree syntaxTree = compilation.SyntaxTrees.FirstOrDefault(tree =>
                string.Equals(tree.FilePath, script.Options.FilePath, StringComparison.OrdinalIgnoreCase))
            ?? compilation.SyntaxTrees.Last();
        SemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree, true);
        SyntaxNode root = syntaxTree.GetRoot();
        return new ScriptCancellationRewriter(semanticModel).Visit(root)!.ToFullString();
    }

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
            return WithRuntimeMethod(rewritten, "Delay");

        if (IsMethod(method, "System.Threading.Thread", "Sleep"))
            return WithRuntimeMethod(rewritten, "Sleep");

        return rewritten;
    }

    private static StatementSyntax WithCancellationCheck(StatementSyntax statement)
    {
        StatementSyntax check = SyntaxFactory.ParseStatement(
            "global::Qx.Scripting.ScriptExecutionContext.ThrowIfCancellationRequested();");
        if (statement is BlockSyntax block)
            return block.WithStatements(block.Statements.Insert(0, check));

        SyntaxTriviaList leadingTrivia = statement.GetLeadingTrivia();
        return SyntaxFactory
            .Block(check, statement.WithoutLeadingTrivia())
            .WithLeadingTrivia(leadingTrivia);
    }

    private static InvocationExpressionSyntax WithRuntimeMethod(
        InvocationExpressionSyntax invocation,
        string method)
    {
        ExpressionSyntax expression = SyntaxFactory.ParseExpression(
            $"global::Qx.Scripting.ScriptExecutionContext.{method}");
        return invocation.WithExpression(expression.WithTriviaFrom(invocation.Expression));
    }

    private static bool IsMethod(IMethodSymbol? method, string containingType, string name) =>
        method?.Name == name &&
        method.ContainingType.ToDisplayString() == containingType;
}
