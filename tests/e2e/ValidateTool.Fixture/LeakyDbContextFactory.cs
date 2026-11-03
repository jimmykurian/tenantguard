// <copyright file="LeakyDbContextFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ValidateTool.Fixture;

public sealed class LeakyDbContextFactory : IDesignTimeDbContextFactory<LeakyDbContext>
{
    public LeakyDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<LeakyDbContext>().UseSqlite("DataSource=:memory:").Options);
}
