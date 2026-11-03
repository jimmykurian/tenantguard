// <copyright file="AppDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Finbuckle.MultiTenant.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

// Finbuckle's IMultiTenantDbContext without MultiTenantDbContext's own checks on SaveChanges: Finbuckle
// resolves the tenant and filters each IsMultiTenant() entity on its shadow TenantId, and TenantGuard
// guards the writes, so a write to another tenant's row throws TenantIsolationViolationException.
internal sealed class AppDbContext(IMultiTenantContextAccessor<TenantInfo> accessor, DbContextOptions<AppDbContext> options)
    : DbContext(options), IMultiTenantDbContext
{
    public TenantInfo? TenantInfo => accessor.MultiTenantContext.TenantInfo;

    ITenantInfo? IMultiTenantDbContext.TenantInfo => TenantInfo;

    // Finbuckle reads these two in EnforceMultiTenant, which this context does not call.
    public TenantMismatchMode TenantMismatchMode => TenantMismatchMode.Throw;

    public TenantNotSetMode TenantNotSetMode => TenantNotSetMode.Throw;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // IsMultiTenant() adds TenantId and the filter but no index on it (TGM009).
        modelBuilder.Entity<Customer>(customer =>
        {
            customer.IsMultiTenant();
            customer.HasIndex("TenantId");
        });
        modelBuilder.Entity<Invoice>(invoice =>
        {
            invoice.IsMultiTenant();
            invoice.HasOne<Customer>().WithMany().HasForeignKey(row => row.CustomerId);
            invoice.HasIndex("TenantId");
        });
    }
}
