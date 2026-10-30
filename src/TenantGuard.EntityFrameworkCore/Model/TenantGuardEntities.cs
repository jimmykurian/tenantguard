// <copyright file="TenantGuardEntities.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// The per-entity metadata stored on the finalized model (SPEC 4.3), so the write guard reads it
// instead of reflecting over user types at save time.
internal sealed class TenantGuardEntities
{
    public const string AnnotationName = "TenantGuard:Entities";

    private readonly Dictionary<IReadOnlyEntityType, TenantPropertyMetadata> _byEntityType;

    private TenantGuardEntities(
        IReadOnlyDictionary<string, string> tenantProperties,
        Dictionary<IReadOnlyEntityType, TenantPropertyMetadata> byEntityType)
    {
        TenantProperties = tenantProperties;
        _byEntityType = byEntityType;
    }

    // Tenant property name by entity type name, for tenant-scoped root entity types.
    public IReadOnlyDictionary<string, string> TenantProperties { get; }

    public bool IsEmpty => _byEntityType.Count == 0;

    // The convention's metadata, or the same computed here for a model the convention never saw,
    // such as a compiled model.
    public static TenantGuardEntities For(IModel model, TenantGuardOptions options)
        => model.GetOrAddRuntimeAnnotationValue(
            AnnotationName,
            static arguments => Create(arguments.Model, TenantModelRules.Check(arguments.Model, arguments.Options).TenantProperties),
            (Model: model, Options: options));

    // Every non-owned entity type whose root is tenant-scoped, derived types included.
    public static TenantGuardEntities Create(IModel model, IReadOnlyDictionary<string, string> tenantProperties)
    {
        var byEntityType = new Dictionary<IReadOnlyEntityType, TenantPropertyMetadata>();
        foreach (var entityType in model.GetEntityTypes())
        {
            var root = entityType.GetRootType();
            if (!entityType.IsOwned()
                && tenantProperties.TryGetValue(root.Name, out var name)
                && root.FindProperty(name) is { } property)
            {
                byEntityType[entityType] = new TenantPropertyMetadata(property, property.GetValueComparer(), property.Sentinel);
            }
        }

        return new TenantGuardEntities(tenantProperties, byEntityType);
    }

    // Owned types are absent, so the write guard skips them (SPEC 5).
    public bool TryGet(IReadOnlyEntityType entityType, out TenantPropertyMetadata tenant)
        => _byEntityType.TryGetValue(entityType, out tenant);
}
