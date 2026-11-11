// <copyright file="TenantMembers.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// Whether a query's entity type may have a tenant filter, under tenantguard.tenant_members_only: TG001
// for IgnoreQueryFilters, and TG002 and TG004 for FromSql on an entity type.
internal static class TenantMembers
{
    // False, under tenantguard.tenant_members_only, when the query's element type is a class or struct
    // that has no member named in tenantguard.tenant_column or marked [TenantKey], itself or through its reference
    // navigations: a tenant filter reaches the tenant through those, as post => post.Blog.TenantId ==
    // TenantId does. Without the key, a shadow tenant property, as Finbuckle's IsMultiTenant() adds,
    // may be there, so every call may switch a tenant filter off.
    public static bool MayHaveTenantFilter(IInvocationOperation invocation, AnalyzerOptions options)
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
}
