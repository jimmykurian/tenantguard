// <copyright file="TenantPropertyDiscovery.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// The tenant property is the first match of an explicit registration, [TenantKey],
// Finbuckle's annotation, then the configured property names.
internal static class TenantPropertyDiscovery
{
    // Finbuckle's Constants.MultiTenantAnnotationName, matched by string so there is no package
    // reference. samples/Finbuckle.Api.Tests checks it against the Finbuckle version the sample uses.
    internal const string FinbuckleMultiTenantAnnotation = "Finbuckle:MultiTenant";

    internal const string FinbuckleTenantPropertyName = "TenantId";

    public static IReadOnlyProperty? Find(IReadOnlyEntityType entityType, TenantGuardOptions options)
        => Find(entityType, options, [.. entityType.GetProperties()]);

    // Only the properties the type declares itself, for derived types (TGM006) and owned types (TGM008).
    public static IReadOnlyProperty? FindDeclared(IReadOnlyEntityType entityType, TenantGuardOptions options)
        => Find(entityType, options, [.. entityType.GetDeclaredProperties()]);

    private static IReadOnlyProperty? Find(
        IReadOnlyEntityType entityType,
        TenantGuardOptions options,
        List<IReadOnlyProperty> properties)
    {
        if (options.ExplicitTenantProperties.TryGetValue(entityType.ClrType, out var explicitName)
            && properties.Find(property => property.Name == explicitName) is { } registered)
        {
            return registered;
        }

        if (properties.Find(HasTenantKey) is { } marked)
        {
            return marked;
        }

        if (entityType.FindAnnotation(FinbuckleMultiTenantAnnotation) is not null
            && properties.Find(property => property.Name == FinbuckleTenantPropertyName) is { } finbuckle)
        {
            return finbuckle;
        }

        foreach (var name in options.TenantPropertyNames)
        {
            if (properties.Find(property => property.Name == name) is { } named)
            {
                return named;
            }
        }

        return null;
    }

    private static bool HasTenantKey(IReadOnlyProperty property)
        => (property.PropertyInfo?.IsDefined(typeof(TenantKeyAttribute), inherit: true) ?? false)
            || (property.FieldInfo?.IsDefined(typeof(TenantKeyAttribute), inherit: true) ?? false);
}
