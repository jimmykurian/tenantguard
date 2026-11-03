// <copyright file="LeakyDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

public sealed class LeakyDbContext(DbContextOptions<LeakyDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
