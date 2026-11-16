// <copyright file="TenantForeignKeyRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// TGM015: a foreign key between two tenant-scoped tables that doesn't pair their tenant properties,
// so the database accepts a row that points at another tenant's row. It runs on a relational
// provider, on the tables TGM009 and TGM010 check.
internal static class TenantForeignKeyRules
{
    public static void Check(IReadOnlyEntityType root, IReadOnlyProperty tenantProperty, TenantGuardOptions options, List<ModelFinding> findings)
    {
        if (!TenantIndexRules.IsTable(root))
        {
            return;
        }

        var foreignKeys = root.GetDerivedTypesInclusive()
            .SelectMany(type => type.GetDeclaredForeignKeys())
            .Where(foreignKey => !foreignKey.IsOwnership)
            .OrderBy(foreignKey => string.Join(",", foreignKey.Properties.Select(column => column.Name)), StringComparer.Ordinal)
            .ThenBy(foreignKey => foreignKey.PrincipalEntityType.DisplayName(), StringComparer.Ordinal);

        foreach (var foreignKey in foreignKeys)
        {
            var principalRoot = foreignKey.PrincipalEntityType.GetRootType();
            if (principalRoot.IsOwned()
                || !TenantIndexRules.IsTable(principalRoot)
                || TenantPropertyDiscovery.Find(principalRoot, options) is not { } principalTenant
                || Finding(foreignKey, tenantProperty.Name, principalTenant.Name) is not { } finding)
            {
                continue;
            }

            findings.Add(finding);
        }
    }

    // Null when the foreign key pairs the dependent's tenant property with the principal's.
    private static ModelFinding? Finding(IReadOnlyForeignKey foreignKey, string property, string principalProperty)
    {
        var pairs = foreignKey.Properties
            .Zip(foreignKey.PrincipalKey.Properties, (dependent, principal) => (Dependent: dependent.Name, Principal: principal.Name))
            .ToList();
        if (pairs.Contains((property, principalProperty)))
        {
            return null;
        }

        var entity = foreignKey.DeclaringEntityType.DisplayName();
        var principalEntity = foreignKey.PrincipalEntityType.DisplayName();
        string[] columns = [.. pairs.Select(pair => pair.Dependent)];

        // The tenant property is in the foreign key, paired with another property of the principal.
        // Dropping that pair would take the other property out of the principal key, so no fix is derived.
        if (pairs.FirstOrDefault(pair => pair.Dependent == property) is { Principal: { } principalColumn })
        {
            return RuleMessages.MispairedTenantForeignKey(entity, property, columns, principalEntity, principalColumn, principalProperty);
        }

        // The fix: the tenant pair, then the foreign key's own pairs without one that already holds the
        // principal's tenant property, so the two lists stay the same length and name no property twice.
        var kept = pairs.Where(pair => pair.Principal != principalProperty).ToList();
        return RuleMessages.TenantFreeForeignKey(
            entity,
            property,
            columns,
            principalEntity,
            [property, .. kept.Select(pair => pair.Dependent)],
            [principalProperty, .. kept.Select(pair => pair.Principal)]);
    }
}
