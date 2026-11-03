// <copyright file="CleanDbContextFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantGuard;

namespace ValidateTool.Fixture;

public sealed class CleanDbContextFactory : IDesignTimeDbContextFactory<CleanDbContext>
{
    public CleanDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<CleanDbContext>()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(tg => tg.CurrentTenant(context => ((CleanDbContext)context).TenantId))
            .Options);
}
