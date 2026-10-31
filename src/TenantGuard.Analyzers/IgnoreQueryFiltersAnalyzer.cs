// <copyright file="IgnoreQueryFiltersAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace TenantGuard.Analyzers;

// TG001: IgnoreQueryFilters() turns off the tenant filter. The named overload is fine when
// tenantguard.tenant_filter_names is set and the names are constants that leave every tenant filter on.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class IgnoreQueryFiltersAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG001";

    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "IgnoreQueryFilters without a justification",
        messageFormat: "IgnoreQueryFilters disables the tenant filter. Add [AllowCrossTenantAccess(\"why\")] to the method, or ignore only the non-tenant filters by name.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions") is { } extensions)
            {
                start.RegisterOperationAction(operation => Analyze(operation, extensions), OperationKind.Invocation);
            }
        });
    }

    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol extensions)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "IgnoreQueryFilters"
            || !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, extensions)
            || LeavesTenantFiltersOn(invocation, context.Options, context.CancellationToken)
            || CrossTenantAccess.IsAllowed(invocation, context.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, CallSite(invocation)));
    }

    // The named overload, with the tenant filter names configured, and constant names that are not among them.
    private static bool LeavesTenantFiltersOn(IInvocationOperation invocation, AnalyzerOptions options, CancellationToken cancellationToken)
    {
        var names = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1);
        if (names is null)
        {
            return false;
        }

        var tenantFilters = AnalyzerSettings.TenantFilterNames(options, invocation.Syntax.SyntaxTree);
        return !tenantFilters.IsEmpty
            && ConstantStrings(names.Value, cancellationToken) is { } ignored
            && !ignored.Any(tenantFilters.Contains);
    }

    // The strings of a collection written out as constants: an array or collection initializer, or a
    // collection expression without a spread. Null for anything else.
    private static List<string>? ConstantStrings(IOperation collection, CancellationToken cancellationToken)
    {
        while (collection is IConversionOperation conversion)
        {
            collection = conversion.Operand;
        }

        IEnumerable<Optional<object?>>? elements = collection switch
        {
            IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues.Select(element => element.ConstantValue),
            IObjectCreationOperation { Initializer: { } initializer } => initializer.Initializers.Select(AddedValue),
            { Syntax: CollectionExpressionSyntax expression, SemanticModel: { } model } => expression.Elements.Select(
                element => element is ExpressionElementSyntax item ? model.GetConstantValue(item.Expression, cancellationToken) : default),
            _ => null,
        };
        if (elements is null)
        {
            return null;
        }

        var strings = new List<string>();
        foreach (var element in elements)
        {
            if (element is not { HasValue: true, Value: string text })
            {
                return null;
            }

            strings.Add(text);
        }

        return strings;
    }

    // The item a collection initializer adds, such as "SoftDelete" in new List<string> { "SoftDelete" }.
    private static Optional<object?> AddedValue(IOperation initializer)
        => initializer is IInvocationOperation { Arguments.Length: 1 } add ? add.Arguments[0].Value.ConstantValue : default;

    // From the method name to the closing parenthesis, so a long query chain is not underlined.
    private static Location CallSite(IInvocationOperation invocation)
    {
        if (invocation.Syntax is not InvocationExpressionSyntax call)
        {
            return invocation.Syntax.GetLocation();
        }

        var name = call.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            var expression => expression,
        };
        return Location.Create(call.SyntaxTree, TextSpan.FromBounds(name.SpanStart, call.Span.End));
    }
}
