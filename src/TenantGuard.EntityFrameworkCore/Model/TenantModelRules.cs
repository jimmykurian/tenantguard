// <copyright file="TenantModelRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Runs rules TGM001 to TGM008 (SPEC 4.2) over every entity type in name order, so the findings are
// deterministic.
internal static class TenantModelRules
{
    public static TenantModelCheck Check(IReadOnlyModel model, TenantGuardOptions options)
    {
        var findings = new List<ModelFinding>();
        var tenantProperties = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entityType in model.GetEntityTypes().OrderBy(entityType => entityType.Name, StringComparer.Ordinal))
        {
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
                CheckRoot(entityType, options, findings, tenantProperties);
            }
        }

        return new TenantModelCheck(new ModelReport(findings), tenantProperties);
    }

    private static void CheckRoot(
        IReadOnlyEntityType entityType,
        TenantGuardOptions options,
        List<ModelFinding> findings,
        Dictionary<string, string> tenantProperties)
    {
        var entity = entityType.DisplayName();
        var tenantProperty = TenantPropertyDiscovery.Find(entityType, options);
        if (tenantProperty is null)
        {
            if (options.RequireAllEntitiesScoped && !options.SharedTypes.Contains(entityType.ClrType))
            {
                findings.Add(RuleMessages.UnscopedEntity(entity, options.TenantPropertyNames[0]));
            }

            return;
        }

        var property = tenantProperty.Name;
        tenantProperties[entityType.Name] = property;

        var filters = QueryFilterAnalysis.Analyze(entityType, tenantProperty);
        if (!filters.ReferencesTenantProperty)
        {
            findings.Add(RuleMessages.UnguardedEntity(entity, property));
        }
        else
        {
            if (filters.CapturesLocal)
            {
                findings.Add(RuleMessages.CapturedValue(entity, property));
            }

            foreach (var literal in filters.Literals)
            {
                findings.Add(RuleMessages.LiteralTenant(entity, property, literal));
            }
        }

        if (tenantProperty.IsNullable)
        {
            findings.Add(RuleMessages.NullableTenant(entity, property));
        }

        if (filters.ReferencesTenantProperty && (filters.HasUnclassifiedOperand || !filters.HasComparison))
        {
            findings.Add(RuleMessages.FilterNotParameterized(entity, property));
        }
    }

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
        if (TenantPropertyDiscovery.FindDeclared(entityType, options) is not { } tenantProperty)
        {
            return;
        }

        var owner = entityType.FindOwnership()?.PrincipalEntityType.DisplayName() ?? "its owner";
        findings.Add(RuleMessages.OwnedTenant(entityType.DisplayName(), owner, tenantProperty.Name));
    }
}
