// <copyright file="CrossTenantModel.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// Whether a model is declared cross-tenant, and how.
internal enum CrossTenantModel
{
    // Tenant-isolated: the rules run, and the write guard checks its saves.
    No,

    // CrossTenant: no rules and no write guard.
    Context,

    // CrossTenantWhenNoTenant, for a model built with no current tenant: no rules, and the write guard
    // blocks a save on it with a current tenant.
    NoTenant,
}
