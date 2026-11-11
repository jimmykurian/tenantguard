// <copyright file="FilterSwitchAnalyzer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// TG005: a framework's own switch turns off the tenant filter, as ABP's IDataFilter.Disable<IMultiTenant>()
// does, through a service rather than IgnoreQueryFilters. ABP's is built in, matched by full name so the
// analyzers take no dependency on ABP; tenantguard.filter_switch_methods adds more.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class FilterSwitchAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TG005";

    public static readonly DiagnosticDescriptor Rule = new(
        Id,
        title: "Framework filter switch without a justification",
        messageFormat: "{0} turns off the tenant filter. Add [AllowCrossTenantAccess(\"why\")] to the method.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    // Covers IDataFilter.Disable<IMultiTenant>() and IDataFilter<IMultiTenant>.Disable().
    private static readonly ImmutableArray<SwitchMethod> _builtIn = ImmutableArray.Create(
        SwitchMethod.Parse("Volo.Abp.Data.IDataFilter.Disable<Volo.Abp.MultiTenancy.IMultiTenant>")!);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.Invocation);
    }

    private static void Analyze(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var configured = AnalyzerSettings.FilterSwitchMethods(context.Options, invocation.Syntax.SyntaxTree).Select(SwitchMethod.Parse).OfType<SwitchMethod>();
        var candidates = Candidates(invocation.TargetMethod, invocation.Instance?.Type as INamedTypeSymbol).ToList();
        if (_builtIn.Concat(configured).FirstOrDefault(entry => candidates.Any(entry.Matches)) is { } matched
            && !CrossTenantAccess.IsAllowed(invocation, context.ContainingSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, CallSite.Of(invocation), matched.DisplayName));
        }
    }

    // The method as called, then each interface method it implements, with the type its type
    // argument comes from when the method has none of its own. The interfaces come from the receiver's
    // type too, which may declare an interface that a base class's method implements.
    private static IEnumerable<(INamedTypeSymbol Type, IMethodSymbol Method)> Candidates(IMethodSymbol method, INamedTypeSymbol? receiver)
    {
        yield return (method.ContainingType, method);
        foreach (var implementer in new[] { receiver, method.ContainingType }.OfType<INamedTypeSymbol>().Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default))
        {
            foreach (var @interface in implementer.AllInterfaces)
            {
                foreach (var member in @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
                {
                    if (SymbolEqualityComparer.Default.Equals(implementer.FindImplementationForInterfaceMember(member)?.OriginalDefinition, method.OriginalDefinition))
                    {
                        yield return (@interface, method);
                    }
                }
            }
        }
    }

    // One entry: Namespace.Type.Method, with an optional <Namespace.TypeArgument>.
    private sealed class SwitchMethod
    {
        private readonly string _typeAndMethod;
        private readonly string? _typeArgument;

        private SwitchMethod(string typeAndMethod, string? typeArgument, string displayName)
        {
            _typeAndMethod = typeAndMethod;
            _typeArgument = typeArgument;
            DisplayName = displayName;
        }

        // Such as IDataFilter.Disable<IMultiTenant>.
        public string DisplayName { get; }

        public static SwitchMethod? Parse(string entry)
        {
            var open = entry.IndexOf('<');
            var typeAndMethod = (open < 0 ? entry : entry.Substring(0, open)).Trim();
            var typeArgument = open < 0 || !entry.EndsWith(">", System.StringComparison.Ordinal) ? null : entry.Substring(open + 1, entry.Length - open - 2).Trim();
            var parts = typeAndMethod.Split('.');
            if (parts.Length < 2 || (open >= 0 && string.IsNullOrEmpty(typeArgument)))
            {
                return null;
            }

            var displayName = parts[parts.Length - 2] + "." + parts[parts.Length - 1]
                + (typeArgument is null ? string.Empty : "<" + typeArgument.Split('.').Last() + ">");
            return new SwitchMethod(typeAndMethod, typeArgument, displayName);
        }

        public bool Matches((INamedTypeSymbol Type, IMethodSymbol Method) candidate)
        {
            if (candidate.Type.ToDisplayString(RawSqlCall.FullName) + "." + candidate.Method.Name != _typeAndMethod)
            {
                return false;
            }

            if (_typeArgument is null)
            {
                return true;
            }

            var typeArguments = candidate.Method.TypeArguments.Length > 0 ? candidate.Method.TypeArguments : candidate.Type.TypeArguments;
            return typeArguments.Length == 1 && typeArguments[0].ToDisplayString(RawSqlCall.FullName) == _typeArgument;
        }
    }
}
