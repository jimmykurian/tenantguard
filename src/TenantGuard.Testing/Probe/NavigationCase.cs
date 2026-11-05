// <copyright file="NavigationCase.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// A navigation the NavigationLeak check covers: declared on, or inherited by, a tenant-scoped entity type,
// and pointing at a tenant-scoped entity type that isn't owned or declared Unfiltered. docs/reference/probe.md lists which count.
internal sealed record NavigationCase(
    IEntityType Type,
    INavigationBase Navigation,
    NavigationKind Kind,
    IEntityType Target,
    IForeignKey? ForeignKey,
    IEntityType? Dependent,
    bool TenantInForeignKey)
{
    public string Name => Navigation.Name;

    public string Check => ProbeChecks.NavigationLeakOf(Name);

    // The navigations of the type to check, by name.
    public static IReadOnlyList<NavigationCase> For(IEntityType type, TenantGuardEntities entities)
    {
        var cases = new List<NavigationCase>();
        foreach (var navigation in type.GetNavigations())
        {
            if (navigation.ForeignKey.IsOwnership || navigation.TargetEntityType.IsOwned()
                || Concrete(navigation.TargetEntityType) is not { } target || !IsProbed(target, entities))
            {
                continue;
            }

            var kind = navigation.IsOnDependent ? NavigationKind.ToPrincipal : NavigationKind.ToDependents;
            var dependent = kind == NavigationKind.ToPrincipal ? type : target;

            // A foreign key that carries the tenant can only point at a principal of the same tenant.
            var tenantInForeignKey = entities.TryGet(dependent, out var tenant) && navigation.ForeignKey.Properties.Contains(tenant.Property);
            cases.Add(new NavigationCase(type, navigation, kind, target, navigation.ForeignKey, dependent, tenantInForeignKey));
        }

        foreach (var skip in type.GetSkipNavigations())
        {
            // A tenant-scoped join type is checked through its own navigations.
            if (Concrete(skip.TargetEntityType) is { } target && IsProbed(target, entities) && !entities.TryGet(skip.JoinEntityType, out _))
            {
                cases.Add(new NavigationCase(type, skip, NavigationKind.Skip, target, null, null, false));
            }
        }

        return [.. cases.OrderBy(navigationCase => navigationCase.Name, StringComparer.Ordinal)];
    }

    // Tenant-scoped and not declared Unfiltered, whose rows are shared across tenants on purpose.
    private static bool IsProbed(IEntityType type, TenantGuardEntities entities)
        => entities.TryGet(type, out var tenant) && !tenant.Unfiltered;

    // The first concrete type of a hierarchy, as the seed plan picks a principal.
    private static IEntityType? Concrete(IEntityType type) => type.GetDerivedTypesInclusive().FirstOrDefault(candidate => !candidate.IsAbstract());
}
