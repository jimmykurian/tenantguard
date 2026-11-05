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
// With tenantguard.tenant_members_only = true, a query over an entity type with no tenant member has no
// tenant filter to turn off.
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
            || !MayHaveTenantFilter(invocation, context.Options)
            || CrossTenantAccess.IsAllowed(invocation, context.ContainingSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, CallSite(invocation)));
    }

    // False, under tenantguard.tenant_members_only, when the query's element type is a class or struct
    // that has no member named in tenantguard.tenant_column or marked [TenantKey], itself or through its reference
    // navigations: a tenant filter reaches the tenant through those, as post => post.Blog.TenantId ==
    // TenantId does. Without the key, a shadow tenant property, as Finbuckle's IsMultiTenant() adds,
    // may be there, so every call may switch a tenant filter off.
    private static bool MayHaveTenantFilter(IInvocationOperation invocation, AnalyzerOptions options)
    {
        if (!AnalyzerSettings.TenantMembersOnly(options, invocation.Syntax.SyntaxTree))
        {
            return true;
        }

        var typeArguments = invocation.TargetMethod.TypeArguments;
        if (typeArguments.Length != 1
            || typeArguments[0] is not INamedTypeSymbol { TypeKind: TypeKind.Class or TypeKind.Struct } entity
            || entity.SpecialType == SpecialType.System_Object)
        {
            return true;
        }

        var columns = AnalyzerSettings.TenantColumns(options, invocation.Syntax.SyntaxTree);
        return ReachesTenantMember(entity, columns, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    private static bool ReachesTenantMember(ITypeSymbol type, ImmutableArray<string> columns, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type))
        {
            return false;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.IsStatic || MemberType(member) is not { } memberType)
                {
                    continue;
                }

                if (IsTenantKey(member) || columns.Any(column => string.Equals(column, member.Name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                if (Navigation(memberType) is { } target
                    && (target.TypeKind is TypeKind.Interface or TypeKind.TypeParameter || ReachesTenantMember(target, columns, visited)))
                {
                    // A navigation whose type can't be followed may lead to a tenant.
                    return true;
                }
            }
        }

        return false;
    }

    // Marked [TenantKey], TenantGuard's attribute for a tenant property under another name.
    private static bool IsTenantKey(ISymbol member)
        => member.GetAttributes().Any(attribute => attribute.AttributeClass is
        {
            Name: "TenantKeyAttribute",
            ContainingNamespace: { Name: "TenantGuard", ContainingNamespace.IsGlobalNamespace: true },
        });

    private static ITypeSymbol? MemberType(ISymbol member)
        => member switch
        {
            IPropertySymbol { IsIndexer: false } property => property.Type,
            IFieldSymbol { IsImplicitlyDeclared: false } field => field.Type,
            _ => null,
        };

    // The type a reference navigation leads to: a class, struct, interface or type parameter of the
    // app's own, other than a collection. Null for a scalar, such as string, int or DateTime.
    private static ITypeSymbol? Navigation(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        var scalarOrCollection = type.SpecialType != SpecialType.None
            || type.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.TypeParameter)
            || IsSystemType(type)
            || type.AllInterfaces.Any(@interface => @interface.SpecialType == SpecialType.System_Collections_IEnumerable);
        return scalarOrCollection ? null : type;
    }

    private static bool IsSystemType(ITypeSymbol type)
    {
        var root = type.ContainingNamespace;
        while (root is { ContainingNamespace.IsGlobalNamespace: false })
        {
            root = root.ContainingNamespace;
        }

        return root is { Name: "System" or "Microsoft" };
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
