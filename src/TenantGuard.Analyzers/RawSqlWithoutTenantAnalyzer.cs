// <copyright file="RawSqlWithoutTenantAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// TG002: raw SQL the compiler can read, a literal, a constant or an interpolated string,
// that mentions none of the tenant columns.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RawSqlWithoutTenantAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG002";

    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Raw SQL without a tenant predicate",
        messageFormat: "Raw SQL bypasses query filters and does not mention '{0}'. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")].",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, RawSqlCall.Kinds);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        var tree = operation.Syntax.SyntaxTree;
        if (RawSqlCall.FindSql(operation, AnalyzerSettings.RawSqlMethods(context.Options, tree)) is not { } sql
            || RawSqlCall.KnownText(sql) is not { } text)
        {
            return;
        }

        var columns = AnalyzerSettings.TenantColumns(context.Options, tree);
        if (columns.Any(column => text.IndexOf(column, StringComparison.OrdinalIgnoreCase) >= 0)
            || CrossTenantAccess.IsAllowed(operation, context.ContainingSymbol))
        {
            return;
        }

        // With several columns the message reads: does not mention 'TenantId' or 'OrgId'.
        context.ReportDiagnostic(Diagnostic.Create(Rule, sql.Syntax.GetLocation(), string.Join("' or '", columns)));
    }
}
