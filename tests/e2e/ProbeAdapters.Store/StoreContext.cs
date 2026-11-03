// <copyright file="StoreContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ProbeAdapters.Store;

// Invoice is filtered on the tenant; Customer has no filter, so it leaks; InvoiceTotal is keyless.
public sealed class StoreContext(DbContextOptions<StoreContext> options, string? tenant) : DbContext(options)
{
    public string? TenantId { get; } = tenant;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(invoice =>
        {
            invoice.HasQueryFilter(row => row.TenantId == TenantId);
            invoice.HasIndex(row => row.TenantId);
        });
        modelBuilder.Entity<Customer>().HasIndex(row => row.TenantId);
        modelBuilder.Entity<InvoiceTotal>(total =>
        {
            total.HasNoKey();
            total.HasQueryFilter(row => row.TenantId == TenantId);
        });
    }
}
