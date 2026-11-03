// <copyright file="CleanDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

public sealed class CleanDbContext(DbContextOptions<CleanDbContext> options) : DbContext(options)
{
    public string? TenantId { get; set; }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
        modelBuilder.Entity<Note>().HasQueryFilter(note => note.TenantId == TenantId);
    }
}
