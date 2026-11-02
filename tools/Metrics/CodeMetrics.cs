// <copyright file="CodeMetrics.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TenantGuard.Tools.Metrics;

// Reads C# files with Roslyn's syntax API: no build and no semantic model, so the numbers
// depend only on the source.
internal static class CodeMetrics
{
    public static FileMetric Measure(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var root = tree.GetCompilationUnitRoot();
        var methods = root.DescendantNodes()
            .Where(IsMethod)
            .Select(node => new MethodMetric(NameOf(node), path, tree.GetLineSpan(node.Span).StartLinePosition.Line + 1, Complexity(node)))
            .ToList();
        return new FileMetric(path, LinesOfCode(tree), methods);
    }

    // Lines that hold at least one token: blank lines and comment-only lines don't count, and a
    // multi-line string counts every line it spans.
    private static int LinesOfCode(SyntaxTree tree)
    {
        var lines = new HashSet<int>();
        foreach (var token in tree.GetRoot().DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.EndOfFileToken))
            {
                continue;
            }

            var span = tree.GetLineSpan(token.Span);
            for (var line = span.StartLinePosition.Line; line <= span.EndLinePosition.Line; line++)
            {
                lines.Add(line);
            }
        }

        return lines.Count;
    }

    private static bool IsMethod(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
        LocalFunctionStatementSyntax => true,
        AccessorDeclarationSyntax accessor => accessor.Body is not null || accessor.ExpressionBody is not null,
        PropertyDeclarationSyntax property => property.ExpressionBody is not null,
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null,
        _ => false,
    };

    // 1 plus every branch: if, loops, case labels, switch arms other than the discard, catch,
    // the conditional operator, &&, ||, ?? and ??=, and and/or patterns. Local functions are
    // measured on their own; lambdas count toward the method that holds them.
    private static int Complexity(SyntaxNode method)
    {
        var complexity = 1;
        var nodes = method.DescendantNodes(child => child == method || child is not LocalFunctionStatementSyntax);
        foreach (var node in nodes)
        {
            complexity += node switch
            {
                IfStatementSyntax or WhileStatementSyntax or DoStatementSyntax or ForStatementSyntax => 1,
                CommonForEachStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax => 1,
                CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax => 1,
                SwitchExpressionArmSyntax arm => arm.Pattern is DiscardPatternSyntax ? 0 : 1,
                BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression ? 1 : 0,
                AssignmentExpressionSyntax assignment => assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression) ? 1 : 0,
                BinaryPatternSyntax => 1,
                _ => 0,
            };
        }

        return complexity;
    }

    // Type.Member, with the enclosing member for a local function: Type.Member.Local.
    private static string NameOf(SyntaxNode node)
    {
        var names = new List<string> { MemberName(node) };
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is BaseTypeDeclarationSyntax type)
            {
                names.Add(type.Identifier.Text);
            }
            else if (IsMethod(parent) && parent is not AccessorDeclarationSyntax)
            {
                names.Add(MemberName(parent));
            }
        }

        if (node.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>() is null)
        {
            names.Add("<top level>");
        }

        names.Reverse();
        return string.Join('.', names);
    }

    private static string MemberName(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax => ".ctor",
        DestructorDeclarationSyntax => "Finalize",
        OperatorDeclarationSyntax op => "operator " + op.OperatorToken.Text,
        ConversionOperatorDeclarationSyntax conversion => "operator " + conversion.Type,
        LocalFunctionStatementSyntax local => local.Identifier.Text,
        AccessorDeclarationSyntax accessor => $"{PropertyName(accessor.Parent?.Parent)}.{accessor.Keyword.Text}",
        PropertyDeclarationSyntax property => property.Identifier.Text,
        IndexerDeclarationSyntax => "this[]",
        _ => node.Kind().ToString(),
    };

    private static string PropertyName(SyntaxNode? node) => node switch
    {
        PropertyDeclarationSyntax property => property.Identifier.Text,
        IndexerDeclarationSyntax => "this[]",
        EventDeclarationSyntax @event => @event.Identifier.Text,
        _ => "?",
    };
}
