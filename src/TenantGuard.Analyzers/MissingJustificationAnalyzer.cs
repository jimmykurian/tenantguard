// <copyright file="MissingJustificationAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// TG003 (SPEC 6): [AllowCrossTenantAccess] with a null, empty or whitespace reason, or with none.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class MissingJustificationAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG003";

    [SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1032:Define diagnostic message correctly", Justification = "SPEC 6 fixes the message, period included.")]
    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "AllowCrossTenantAccess without a reason",
        messageFormat: "AllowCrossTenantAccess needs a reason.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Attribute);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (!MayNameTheAttribute(attribute.Name))
        {
            return;
        }

        // An attribute with no reason does not bind to the constructor, so the candidates count too.
        var symbol = context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken);
        var constructor = symbol.Symbol ?? symbol.CandidateSymbols.FirstOrDefault();
        if (!CrossTenantAccess.IsAttribute(constructor?.ContainingType))
        {
            return;
        }

        var reason = ReasonArgument(attribute);
        var value = reason is null ? default : context.SemanticModel.GetConstantValue(reason.Expression, context.CancellationToken);
        if (reason is null || (value.HasValue && string.IsNullOrWhiteSpace(value.Value as string)))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, attribute.GetLocation()));
        }
    }

    // Checked before binding, which costs more: the last part of the name, with or without the suffix.
    private static bool MayNameTheAttribute(NameSyntax name)
    {
        var simpleName = name switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name,
            _ => name as SimpleNameSyntax,
        };
        return simpleName?.Identifier.ValueText is "AllowCrossTenantAccess" or CrossTenantAccess.AttributeName;
    }

    // The constructor's reason, or a Reason property set by name on an attribute a project declares.
    private static AttributeArgumentSyntax? ReasonArgument(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { } list)
        {
            return null;
        }

        return list.Arguments.FirstOrDefault(argument => argument.NameEquals is null)
            ?? list.Arguments.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Reason");
    }
}
