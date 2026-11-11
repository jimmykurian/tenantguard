// <copyright file="UnverifiableRawSqlAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// TG004: raw SQL built at runtime, which no analyzer can check for a tenant predicate.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UnverifiableRawSqlAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG004";

    [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1032:Define diagnostic message correctly", Justification = "The documented message ends with a period.")]
    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Raw SQL that cannot be checked for a tenant predicate",
        messageFormat: "Raw SQL built at runtime cannot be checked for a tenant predicate.",
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
        if (RawSqlCall.FindSql(operation, context.Options) is { } sql
            && RawSqlCall.KnownText(sql) is null
            && !CrossTenantAccess.IsAllowed(operation, context.ContainingSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, sql.Syntax.GetLocation()));
        }
    }
}
