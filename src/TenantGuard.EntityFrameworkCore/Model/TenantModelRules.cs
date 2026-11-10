// <copyright file="TenantModelRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Runs rules TGM001 to TGM014 over every entity type in name order, so the findings are
// deterministic.
internal static class TenantModelRules
{
    // relational says whether the model's provider is relational, for TGM009 and TGM010; when it's
    // null, the model's annotations tell. capturedValuesAllowed leaves TGM003 out, when the context
    // passed the AllowCapturedValues check (PerTenantModel). noTenantModel says the context building the
    // model passed the CrossTenantWhenNoTenant check.
    public static TenantModelCheck Check(
        IReadOnlyModel model,
        TenantGuardOptions options,
        Type? contextType = null,
        bool? relational = null,
        bool capturedValuesAllowed = false,
        bool noTenantModel = false)
    {
        if (options.CrossTenantReason is { } reason)
        {
            return CrossTenant(model, options, reason, CrossTenantModel.Context);
        }

        if (noTenantModel && options.CrossTenantWhenNoTenantReason is { } noTenantReason)
        {
            return CrossTenant(model, options, noTenantReason, CrossTenantModel.NoTenant);
        }

        var checksIndexes = relational ?? RelationalProvider.Built(model);
        var findings = new List<ModelFinding>();
        var suppressed = new List<ModelReport.SuppressedFinding>();
        var unfiltered = new List<ModelReport.Declaration>();
        var tenantProperties = new Dictionary<string, string>(StringComparer.Ordinal);

        // TGM011 reports a view only in a model that has tenant data.
        var hasTenantData = model.GetEntityTypes().Any(entityType => !entityType.IsOwned()
            && entityType.BaseType is null
            && TenantPropertyDiscovery.Find(entityType, options) is not null);

        foreach (var entityType in model.GetEntityTypes().OrderBy(entityType => entityType.Name, StringComparer.Ordinal))
        {
            var start = findings.Count;
            if (entityType.IsOwned())
            {
                CheckOwned(entityType, options, findings);
            }
            else if (entityType.BaseType is not null)
            {
                CheckDerived(entityType, options, findings);
            }
            else
            {
                CheckRoot(entityType, options, findings, tenantProperties, unfiltered, contextType, checksIndexes, capturedValuesAllowed, hasTenantData);
            }

            Suppress(entityType, options, findings, start, suppressed);
        }

        return new TenantModelCheck(
            new ModelReport(findings, suppressed, unfiltered),
            tenantProperties,
            unfiltered.Select(declaration => declaration.Entity).ToHashSet(StringComparer.Ordinal),
            CrossTenantModel.No);
    }

    // No rule runs. The no-tenant model keeps its tenant properties, so the write guard can tell a save
    // of tenant-scoped rows on it.
    private static TenantModelCheck CrossTenant(IReadOnlyModel model, TenantGuardOptions options, string reason, CrossTenantModel kind)
    {
        var tenantProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (kind == CrossTenantModel.NoTenant)
        {
            foreach (var entityType in model.GetEntityTypes().Where(entityType => !entityType.IsOwned() && entityType.BaseType is null))
            {
                if (TenantPropertyDiscovery.Find(entityType, options) is { } tenantProperty)
                {
                    tenantProperties[entityType.Name] = tenantProperty.Name;
                }
            }
        }

        return new TenantModelCheck(new ModelReport([], crossTenantReason: reason), tenantProperties, new HashSet<string>(StringComparer.Ordinal), kind);
    }

    // Moves the findings an entity type's rules added, from start on, that Suppress leaves out.
    private static void Suppress(
        IReadOnlyEntityType entityType,
        TenantGuardOptions options,
        List<ModelFinding> findings,
        int start,
        List<ModelReport.SuppressedFinding> suppressed)
    {
        if (options.Suppressions.IsEmpty && options.TypeSuppressions.IsEmpty)
        {
            return;
        }

        for (var index = start; index < findings.Count;)
        {
            if (options.SuppressionReason(entityType.ClrType, findings[index].RuleId) is { } reason)
            {
                suppressed.Add(new ModelReport.SuppressedFinding(findings[index], reason));
                findings.RemoveAt(index);
            }
            else
            {
                index++;
            }
        }
    }

    private static void CheckRoot(
        IReadOnlyEntityType entityType,
        TenantGuardOptions options,
        List<ModelFinding> findings,
        Dictionary<string, string> tenantProperties,
        List<ModelReport.Declaration> unfiltered,
        Type? contextType,
        bool checksIndexes,
        bool capturedValuesAllowed,
        bool hasTenantData)
    {
        var entity = entityType.DisplayName();
        var tenantProperty = TenantPropertyDiscovery.Find(entityType, options);
        if (tenantProperty is null)
        {
            CheckUnscoped(entityType, options, findings, checksIndexes, hasTenantData);
            return;
        }

        var property = tenantProperty.Name;
        tenantProperties[entityType.Name] = property;

        // Unfiltered on purpose: the rules about its filter (TGM001, TGM003, TGM004, TGM007) don't apply.
        var checksFilter = !options.UnfilteredTypes.TryGetValue(entityType.ClrType, out var reason);
        if (!checksFilter)
        {
            unfiltered.Add(new ModelReport.Declaration(entity, reason!));
        }

        var filters = QueryFilterAnalysis.Analyze(entityType, tenantProperty, contextType);
        if (checksFilter && !filters.ReferencesTenantProperty)
        {
            findings.Add(RuleMessages.UnguardedEntity(entity, property));
        }
        else if (checksFilter)
        {
            if (filters.CapturesLocal && !capturedValuesAllowed)
            {
                findings.Add(RuleMessages.CapturedValue(entity, property));
            }

            foreach (var literal in filters.Literals)
            {
                findings.Add(RuleMessages.LiteralTenant(entity, property, literal));
            }
        }

        if (tenantProperty.IsNullable && !options.NullTenantIsHost)
        {
            findings.Add(RuleMessages.NullableTenant(entity, property));
        }

        if (checksFilter && filters.ReferencesTenantProperty && (filters.HasUnclassifiedOperand || !filters.HasComparison))
        {
            findings.Add(RuleMessages.FilterNotParameterized(entity, property));
        }

        if (checksFilter && filters.ReferencesTenantProperty)
        {
            var (switchable, openWithoutTenant) = FilterSwitches.Analyze(entityType, tenantProperty);
            if (switchable)
            {
                findings.Add(RuleMessages.SwitchableFilter(entity, property));
            }

            if (openWithoutTenant)
            {
                findings.Add(RuleMessages.OpenWithoutTenant(entity, property));
            }
        }

        if (checksIndexes)
        {
            TenantIndexRules.Check(entityType, tenantProperty, options, findings);
        }
    }

    // A root with no tenant property: TGM002 under RequireAllEntitiesScoped, else TGM011 for a view and
    // TGM014 for a dependent of a tenant-scoped principal. A shared type gets none of them.
    private static void CheckUnscoped(
        IReadOnlyEntityType entityType,
        TenantGuardOptions options,
        List<ModelFinding> findings,
        bool checksIndexes,
        bool hasTenantData)
    {
        var entity = entityType.DisplayName();
        if (options.SharedTypes.Contains(entityType.ClrType))
        {
            return;
        }

        if (options.RequireAllEntitiesScoped)
        {
            findings.Add(RuleMessages.UnscopedEntity(entity, options.TenantPropertyNames[0]));
            return;
        }

        if (checksIndexes && hasTenantData && TenantIndexRules.NonTableSource(entityType) is { } source)
        {
            findings.Add(RuleMessages.TenantFreeView(entity, source, options.TenantPropertyNames[0]));
        }

        if (IsImplicitJoin(entityType))
        {
            return;
        }

        var principal = entityType.GetDerivedTypesInclusive()
            .SelectMany(type => type.GetDeclaredForeignKeys())
            .Where(foreignKey => foreignKey.IsRequired
                && !foreignKey.IsOwnership
                && !foreignKey.PrincipalEntityType.IsOwned()
                && TenantPropertyDiscovery.Find(foreignKey.PrincipalEntityType.GetRootType(), options) is not null)
            .OrderBy(foreignKey => foreignKey.PrincipalEntityType.GetRootType().DisplayName(), StringComparer.Ordinal)
            .ThenBy(foreignKey => string.Join(",", foreignKey.Properties.Select(column => column.Name)), StringComparer.Ordinal)
            .FirstOrDefault();
        if (principal is not null && !QueryFilterAnalysis.ReachesTenantProperty(entityType, options))
        {
            findings.Add(RuleMessages.UnscopedDependent(
                entity,
                principal.PrincipalEntityType.GetRootType().DisplayName(),
                [.. principal.Properties.Select(column => column.Name)],
                options.TenantPropertyNames[0]));
        }
    }

    // The join entity type EF Core creates for a many-to-many relationship: a shared
    // Dictionary<string, object> type that a skip navigation joins through.
    private static bool IsImplicitJoin(IReadOnlyEntityType entityType)
        => entityType.HasSharedClrType
            && entityType.ClrType == typeof(Dictionary<string, object>)
            && entityType.Model.GetEntityTypes().SelectMany(type => type.GetDeclaredSkipNavigations()).Any(skip => skip.JoinEntityType == entityType);

    private static void CheckDerived(IReadOnlyEntityType entityType, TenantGuardOptions options, List<ModelFinding> findings)
    {
        var root = entityType.GetRootType();
        if (TenantPropertyDiscovery.Find(root, options) is not null)
        {
            return;
        }

        if (TenantPropertyDiscovery.FindDeclared(entityType, options) is { } declared)
        {
            findings.Add(RuleMessages.TenantOnDerived(entityType.DisplayName(), declared.Name, root.DisplayName()));
        }
    }

    private static void CheckOwned(IReadOnlyEntityType entityType, TenantGuardOptions options, List<ModelFinding> findings)
    {
        if (TenantPropertyDiscovery.FindOwned(entityType, options) is not { } tenantProperty)
        {
            return;
        }

        var owner = entityType.FindOwnership()?.PrincipalEntityType.DisplayName() ?? "its owner";
        findings.Add(RuleMessages.OwnedTenant(entityType.DisplayName(), owner, tenantProperty.Name));
    }
}
