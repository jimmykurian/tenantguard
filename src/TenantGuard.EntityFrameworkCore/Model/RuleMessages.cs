// <copyright file="RuleMessages.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// The rule IDs, severities and messages are public and documented. Changing one is a breaking change.
internal static class RuleMessages
{
    public static ModelFinding UnguardedEntity(string entity, string property)
        => new(
            "TGM001",
            Severity.Error,
            entity,
            property,
            $"{entity} has tenant property '{property}' but no query filter references it. Add HasQueryFilter(e => e.{property} == <current tenant>) on the root type.");

    public static ModelFinding UnscopedEntity(string entity, string suggestedProperty)
        => new(
            "TGM002",
            Severity.Error,
            entity,
            null,
            $"{entity} has no tenant property and is not declared shared. Add '{suggestedProperty}', or declare it with Shared<{entity}>().");

    public static ModelFinding CapturedValue(string entity, string property)
        => new(
            "TGM003",
            Severity.Error,
            entity,
            property,
            $"{entity}: the tenant filter captures a local value. The model is cached per context type, so every tenant will see the first tenant's rows. Reference a DbContext member instead.");

    public static ModelFinding LiteralTenant(string entity, string property, string value)
        => new(
            "TGM004",
            Severity.Error,
            entity,
            property,
            $"{entity}: the tenant filter compares '{property}' to the constant {value}. Reference a DbContext member instead.");

    public static ModelFinding NullableTenant(string entity, string property)
        => new(
            "TGM005",
            Severity.Warning,
            entity,
            property,
            $"{entity}.{property} is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.");

    public static ModelFinding TenantOnDerived(string entity, string property, string root)
        => new(
            "TGM006",
            Severity.Error,
            entity,
            property,
            $"{entity} declares '{property}' but filters can only be defined on the root type {root}. Move the property to the root.");

    public static ModelFinding FilterNotParameterized(string entity, string property)
        => new(
            "TGM007",
            Severity.Warning,
            entity,
            property,
            $"{entity}: the tenant filter's operand could not be classified; confirm it is evaluated per query.");

    public static ModelFinding OwnedTenant(string owned, string owner, string property)
        => new(
            "TGM008",
            Severity.Warning,
            owned,
            property,
            $"{owned} is owned by {owner}; its '{property}' is ignored, isolation follows the owner.");

    public static ModelFinding UnindexedTenant(string entity, string property)
        => new(
            "TGM009",
            Severity.Warning,
            entity,
            property,
            $"{entity} has no index or key that starts with '{property}', so each query its tenant filter scopes reads past other tenants' rows. Add HasIndex(e => e.{property}), or put '{property}' first in an existing index or key.");

    public static ModelFinding TenantFreeUniqueIndex(string entity, string property, IReadOnlyList<string> columns)
        => new(
            "TGM010",
            Severity.Error,
            entity,
            property,
            $"{entity}: the unique index on ({string.Join(", ", columns)}) doesn't include '{property}', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasIndex(e => new {{ e.{property}, {Members(columns)} }}).IsUnique().");

    public static ModelFinding TenantFreeAlternateKey(string entity, string property, IReadOnlyList<string> columns)
        => new(
            "TGM010",
            Severity.Error,
            entity,
            property,
            $"{entity}: the alternate key ({string.Join(", ", columns)}) doesn't include '{property}', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasAlternateKey(e => new {{ e.{property}, {Members(columns)} }}).");

    public static ModelFinding TenantFreePrimaryKey(string entity, string property, IReadOnlyList<string> columns)
        => new(
            "TGM010",
            Severity.Error,
            entity,
            property,
            $"{entity}: the primary key ({string.Join(", ", columns)}) doesn't include '{property}' and its values aren't generated, so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasKey(e => new {{ e.{property}, {Members(columns)} }}).");

    // source is "the view 'name'", "a SQL query" or "the function 'name'".
    public static ModelFinding TenantFreeView(string entity, string source, string suggestedProperty)
        => new(
            "TGM011",
            Severity.Warning,
            entity,
            null,
            $"{entity} is mapped to {source} and has no tenant property, so it returns every tenant's rows. Select '{suggestedProperty}' in it and filter on it, or declare it with Shared<{entity}>().");

    public static ModelFinding SwitchableFilter(string entity, string property)
        => new(
            "TGM012",
            Severity.Warning,
            entity,
            property,
            $"{entity}: the tenant filter can be switched off: it is ORed with a condition that doesn't read the row, and while that condition is true, every tenant's rows are returned. Confirm it is true only on purpose.");

    public static ModelFinding OpenWithoutTenant(string entity, string property)
        => new(
            "TGM013",
            Severity.Warning,
            entity,
            property,
            $"{entity}: the tenant filter returns every tenant's rows when there is no current tenant: it is ORed with a null check of the value '{property}' is compared with. Compare '{property}' with the current tenant alone, so a context with no tenant sees no tenant's rows.");

    public static ModelFinding UnscopedDependent(string entity, string principal, IReadOnlyList<string> columns, string suggestedProperty)
        => new(
            "TGM014",
            Severity.Warning,
            entity,
            null,
            $"{entity} has no tenant property and no query filter that reaches one, but its rows belong to tenant-scoped {principal} through the required foreign key ({string.Join(", ", columns)}), so Set<{entity}>() returns every tenant's rows. Filter it through {principal}, give it '{suggestedProperty}' and a filter, or declare it with Shared<{entity}>().");

    private static string Members(IReadOnlyList<string> columns)
        => string.Join(", ", columns.Select(column => "e." + column));
}
