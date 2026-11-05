// <copyright file="TenantGuardEntities.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// The per-entity metadata stored on the finalized model, so the write guard reads it
// instead of reflecting over user types at save time.
internal sealed class TenantGuardEntities
{
    public const string AnnotationName = "TenantGuard:Entities";

    private readonly Dictionary<IReadOnlyEntityType, TenantPropertyMetadata> _byEntityType;

    private TenantGuardEntities(
        IReadOnlyDictionary<string, string> tenantProperties,
        Dictionary<IReadOnlyEntityType, TenantPropertyMetadata> byEntityType,
        object? modelTenant,
        CrossTenantModel crossTenant)
    {
        TenantProperties = tenantProperties;
        _byEntityType = byEntityType;
        ModelTenant = modelTenant;
        CrossTenant = crossTenant;
    }

    // Tenant property name by entity type name, for tenant-scoped root entity types.
    public IReadOnlyDictionary<string, string> TenantProperties { get; }

    public bool IsEmpty => _byEntityType.Count == 0;

    // The tenant a per-tenant model was built for, under AllowCapturedValues, or null.
    public object? ModelTenant { get; }

    // Whether the model is declared cross-tenant (CrossTenant, CrossTenantWhenNoTenant).
    public CrossTenantModel CrossTenant { get; }

    // The convention's metadata, or the same computed here for a model the convention never saw,
    // such as a compiled model.
    public static TenantGuardEntities For(IModel model, TenantGuardOptions options)
        => model.GetOrAddRuntimeAnnotationValue(
            AnnotationName,
            static arguments => Create(arguments.Model, TenantModelRules.Check(arguments.Model, arguments.Options)),
            (Model: model, Options: options));

    // Every non-owned entity type whose root is tenant-scoped, derived types included.
    public static TenantGuardEntities Create(IModel model, TenantModelCheck check, object? modelTenant = null)
    {
        var byEntityType = new Dictionary<IReadOnlyEntityType, TenantPropertyMetadata>();
        foreach (var entityType in model.GetEntityTypes())
        {
            var root = entityType.GetRootType();
            if (!entityType.IsOwned()
                && check.TenantProperties.TryGetValue(root.Name, out var name)
                && root.FindProperty(name) is { } property)
            {
                byEntityType[entityType] = new TenantPropertyMetadata(
                    property,
                    property.GetValueComparer(),
                    property.Sentinel,
                    check.Unfiltered.Contains(root.DisplayName()));
            }
        }

        return new TenantGuardEntities(check.TenantProperties, byEntityType, modelTenant, check.CrossTenant);
    }

    // Owned types are absent, so the write guard skips them.
    public bool TryGet(IReadOnlyEntityType entityType, out TenantPropertyMetadata tenant)
        => _byEntityType.TryGetValue(entityType, out tenant);

    // By entity type name, for an entry of another model of the same context type, as the probe meets
    // with a model per tenant. The metadata is this model's.
    public bool TryGet(string entityTypeName, out TenantPropertyMetadata tenant)
    {
        foreach (var (entityType, metadata) in _byEntityType)
        {
            if (entityType.Name == entityTypeName)
            {
                tenant = metadata;
                return true;
            }
        }

        tenant = default;
        return false;
    }
}
