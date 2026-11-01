// <copyright file="ITenantSource.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Where a filter reads the tenant: the context itself, the way Finbuckle's IMultiTenantDbContext does,
// or a separate object.
internal interface ITenantSource
{
    string? TenantId { get; }
}
