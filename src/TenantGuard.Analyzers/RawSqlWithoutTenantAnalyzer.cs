// <copyright file="RawSqlWithoutTenantAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// TG002: raw SQL the compiler can read, a literal, a constant, an interpolated string or a local set
// once to one, that compares none of the tenant columns with a value.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class RawSqlWithoutTenantAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG002";

    [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1032:Define diagnostic message correctly", Justification = "{1} is empty or the documented hint, which ends with a period.")]
    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Raw SQL without a tenant predicate",
        messageFormat: "Raw SQL bypasses query filters and does not compare '{0}' with a value. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")].{1}",
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
        if (RawSqlCall.FindSql(operation, context.Options) is not { } sql
            || RawSqlCall.KnownText(sql) is not { } text)
        {
            return;
        }

        var columns = AnalyzerSettings.TenantColumns(context.Options, tree);
        if (TenantComparison.ComparesAny(text, columns)
            || CrossTenantAccess.IsAllowed(operation, context.ContainingSymbol))
        {
            return;
        }

        // With several columns the message reads: does not compare 'TenantId' or 'OrgId' with a value.
        // With the default column, it ends with a hint, for an app whose tenant property has another name.
        var hint = AnalyzerSettings.TenantColumnIsSet(context.Options, tree) ? string.Empty : " If the tenant column has another name, set tenantguard.tenant_column.";
        context.ReportDiagnostic(Diagnostic.Create(Rule, sql.Syntax.GetLocation(), string.Join("' or '", columns), hint));
    }
}
