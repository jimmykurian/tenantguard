// <copyright file="TenantIndexRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// TGM009 and TGM010: the indexes and keys of a tenant-scoped table, on a relational provider. The
// relational annotations are read by name, so the runtime package needs no relational reference.
// TGM011 reads the same annotations for the types these rules skip.
internal static class TenantIndexRules
{
    private static readonly string[] _nonTableMappings = ["Relational:ViewName", "Relational:SqlQuery", "Relational:FunctionName"];

    // What the type is mapped to when it isn't a table, as TGM011 names it: "the view 'name'", "a SQL
    // query" or "the function 'name'"; null for a table.
    public static string? NonTableSource(IReadOnlyEntityType root)
    {
        if (root.FindAnnotation("Relational:ViewName")?.Value is string view)
        {
            return $"the view '{view}'";
        }

        if (root.FindAnnotation("Relational:SqlQuery")?.Value is not null)
        {
            return "a SQL query";
        }

        return root.FindAnnotation("Relational:FunctionName")?.Value is string function ? $"the function '{function}'" : null;
    }

    public static void Check(IReadOnlyEntityType root, IReadOnlyProperty tenantProperty, TenantGuardOptions options, List<ModelFinding> findings)
    {
        if (root.FindPrimaryKey() is null || IsMappedToNonTable(root))
        {
            return;
        }

        var property = tenantProperty.Name;
        var hierarchy = root.GetDerivedTypesInclusive().ToList();
        var keys = hierarchy.SelectMany(type => type.GetDeclaredKeys()).ToList();
        var indexes = hierarchy.SelectMany(type => type.GetDeclaredIndexes()).ToList();

        if (!keys.Any(key => StartsWith(key.Properties, property)) && !indexes.Any(index => StartsWith(index.Properties, property)))
        {
            findings.Add(RuleMessages.UnindexedTenant(root.DisplayName(), property));
        }

        // A natural primary key: no property's values are generated. A single Guid is random, so
        // another tenant can't reuse it.
        if (root.FindPrimaryKey() is { } primaryKey
            && !Includes(primaryKey.Properties, property)
            && primaryKey.Properties.All(column => column.ValueGenerated == ValueGenerated.Never)
            && !IsSingleGuid(primaryKey.Properties)
            && !CoversTenantScopedForeignKey(root, primaryKey.Properties, options))
        {
            findings.Add(RuleMessages.TenantFreePrimaryKey(root.DisplayName(), property, Names(primaryKey.Properties)));
        }

        foreach (var key in keys.Where(key => !key.IsPrimaryKey()
            && !Includes(key.Properties, property)
            && !CoversTenantScopedForeignKey(key.DeclaringEntityType, key.Properties, options)))
        {
            findings.Add(RuleMessages.TenantFreeAlternateKey(key.DeclaringEntityType.DisplayName(), property, Names(key.Properties)));
        }

        foreach (var index in indexes.Where(index => index.IsUnique
            && !Includes(index.Properties, property)
            && !BacksForeignKey(index)
            && !CoversTenantScopedForeignKey(index.DeclaringEntityType, index.Properties, options)))
        {
            findings.Add(RuleMessages.TenantFreeUniqueIndex(index.DeclaringEntityType.DisplayName(), property, Names(index.Properties)));
        }
    }

    private static bool IsMappedToNonTable(IReadOnlyEntityType root)
        => _nonTableMappings.Any(name => root.FindAnnotation(name)?.Value is not null);

    // A one-to-one foreign key's unique index holds keys of the principal's rows, which are unique
    // across tenants like the primary key.
    private static bool BacksForeignKey(IReadOnlyIndex index)
        => index.DeclaringEntityType.GetForeignKeys().Any(foreignKey => foreignKey.Properties.SequenceEqual(index.Properties));

    // A unique index or alternate key that holds every column of a foreign key to a tenant-scoped
    // principal is unique within that principal's rows, each of which belongs to one tenant, so
    // another tenant can't collide with a value it can't reference.
    private static bool CoversTenantScopedForeignKey(
        IReadOnlyEntityType entityType,
        IReadOnlyList<IReadOnlyPropertyBase> properties,
        TenantGuardOptions options)
        => entityType.GetForeignKeys().Any(foreignKey => foreignKey.Properties.All(column => properties.Contains(column))
            && !foreignKey.PrincipalEntityType.IsOwned()
            && TenantPropertyDiscovery.Find(foreignKey.PrincipalEntityType.GetRootType(), options) is not null);

    private static bool IsSingleGuid(IReadOnlyList<IReadOnlyProperty> properties)
        => properties is [var only] && (Nullable.GetUnderlyingType(only.ClrType) ?? only.ClrType) == typeof(Guid);

    private static bool StartsWith(IReadOnlyList<IReadOnlyPropertyBase> properties, string property)
        => properties.Count > 0 && properties[0].Name == property;

    private static bool Includes(IReadOnlyList<IReadOnlyPropertyBase> properties, string property)
        => properties.Any(candidate => candidate.Name == property);

    private static string[] Names(IReadOnlyList<IReadOnlyPropertyBase> properties)
        => [.. properties.Select(candidate => candidate.Name)];
}
