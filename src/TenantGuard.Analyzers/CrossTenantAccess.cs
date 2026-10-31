// <copyright file="CrossTenantAccess.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// [AllowCrossTenantAccess("why")], matched by full name, so the analyzers need no reference
// to the runtime package and a project can declare the attribute itself. It counts only with a
// reason; TG003 reports one without.
internal static class CrossTenantAccess
{
    public const string AttributeName = "AllowCrossTenantAccessAttribute";

    // Whether an enclosing lambda or local function, the method, or a containing type carries the
    // attribute with a reason.
    public static bool IsAllowed(IOperation operation, ISymbol containingSymbol)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            var function = parent switch
            {
                IAnonymousFunctionOperation lambda => lambda.Symbol,
                ILocalFunctionOperation localFunction => localFunction.Symbol,
                _ => null,
            };
            if (function is not null && HasReason(function))
            {
                return true;
            }
        }

        for (var symbol = containingSymbol; symbol is not null and not INamespaceSymbol; symbol = symbol.ContainingSymbol)
        {
            if (symbol is IMethodSymbol or INamedTypeSymbol && HasReason(symbol))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsAttribute(INamedTypeSymbol? type)
        => type is { Name: AttributeName, ContainingNamespace: { Name: "TenantGuard", ContainingNamespace.IsGlobalNamespace: true } };

    private static bool HasReason(ISymbol symbol)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (IsAttribute(attribute.AttributeClass) && !string.IsNullOrWhiteSpace(Reason(attribute)))
            {
                return true;
            }
        }

        return false;
    }

    // The constructor's reason, or a Reason property set by name on an attribute a project declares.
    private static string? Reason(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 0)
        {
            return attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: string reason } ? reason : null;
        }

        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Reason" && argument.Value is { Kind: TypedConstantKind.Primitive, Value: string reason })
            {
                return reason;
            }
        }

        return null;
    }
}
