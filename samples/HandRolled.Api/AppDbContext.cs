// <copyright file="AppDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

// Every tenant-scoped entity is filtered on TenantId, a member of the context, so each context instance
// sees only its own tenant's rows.
internal sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    // The tenant this context reads and writes as.
    public string? TenantId => tenant.TenantId;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>().HasQueryFilter(customer => customer.TenantId == TenantId);
        modelBuilder.Entity<Invoice>(invoice =>
        {
            invoice.HasOne<Customer>().WithMany().HasForeignKey(row => row.CustomerId);
            invoice.HasQueryFilter(row => row.TenantId == TenantId);
        });
    }
}
