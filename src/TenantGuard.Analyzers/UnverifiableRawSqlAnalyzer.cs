// <copyright file="UnverifiableRawSqlAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// TG004 (SPEC 6): raw SQL built at runtime, which no analyzer can check for a tenant predicate.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class UnverifiableRawSqlAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG004";

    [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1032:Define diagnostic message correctly", Justification = "SPEC 6 fixes the message, period included.")]
    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Raw SQL that cannot be checked for a tenant predicate",
        messageFormat: "Raw SQL built at runtime cannot be checked for a tenant predicate.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (RawSqlCall.FindExtensions(start.Compilation) is { } extensions)
            {
                start.RegisterOperationAction(operation => Analyze(operation, extensions), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol extensions)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (RawSqlCall.FindSql(invocation, extensions) is { } sql
            && RawSqlCall.KnownText(sql) is null
            && !CrossTenantAccess.IsAllowed(invocation, context.ContainingSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, sql.Syntax.GetLocation()));
        }
    }
}
