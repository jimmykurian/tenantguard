// <copyright file="GuardedDbContextFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantGuard;

namespace ValidateTool.Fixture;

public sealed class GuardedDbContextFactory : IDesignTimeDbContextFactory<GuardedDbContext>
{
    public GuardedDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<GuardedDbContext>()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(tg => tg.CurrentTenant(context => ((GuardedDbContext)context).TenantId))
            .Options);
}
