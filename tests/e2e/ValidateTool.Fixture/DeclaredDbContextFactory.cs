// <copyright file="DeclaredDbContextFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TenantGuard;

namespace ValidateTool.Fixture;

public sealed class DeclaredDbContextFactory : IDesignTimeDbContextFactory<DeclaredDbContext>
{
    public DeclaredDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<DeclaredDbContext>()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(tg => tg
                .Unfiltered<Membership>("maps users to their tenants")
                .Suppress<Note>("TGM005", "a note without a tenant is a draft")
                .CurrentTenant(context => ((DeclaredDbContext)context).TenantId))
            .Options);
}
