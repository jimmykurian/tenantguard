// <copyright file="TenantGuardEntities.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// The per-entity metadata the convention stores on the finalized model (SPEC 4.3), so later stages
// read it instead of reflecting over user types.
internal sealed class TenantGuardEntities(IReadOnlyDictionary<string, string> tenantProperties)
{
    public const string AnnotationName = "TenantGuard:Entities";

    // Tenant property name by entity type name, for tenant-scoped root entity types.
    public IReadOnlyDictionary<string, string> TenantProperties { get; } = tenantProperties;
}
