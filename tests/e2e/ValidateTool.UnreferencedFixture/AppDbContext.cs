// <copyright file="AppDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.UnreferencedFixture;

// Configures itself in OnConfiguring, with no design-time factory.
public sealed class AppDbContext : DbContext
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("DataSource=:memory:");
}
