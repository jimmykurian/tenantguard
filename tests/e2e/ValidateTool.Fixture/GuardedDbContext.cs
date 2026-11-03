// <copyright file="GuardedDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

// UseTenantGuard under the default Enforcement.Throw, and Order has no query filter: the EF
// convention throws while the model builds, TGM001.
public sealed class GuardedDbContext(DbContextOptions<GuardedDbContext> options) : DbContext(options)
{
    public string? TenantId { get; set; }

    public DbSet<Order> Orders => Set<Order>();
}
