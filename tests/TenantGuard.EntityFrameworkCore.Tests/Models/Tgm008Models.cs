// <copyright file="Tgm008Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM008 owned-tenant: an owned type has a tenant property, which isolation ignores.
internal static class Tgm008Models
{
    // Positive: an owned type without a tenant property.
    internal sealed class OwnedWithoutTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>(order =>
            {
                order.HasQueryFilter(row => row.TenantId == CurrentTenant);
                order.OwnsOne(row => row.Shipping);
            });
    }

    // Negative: the owned type carries its own TenantId.
    internal sealed class OwnedWithTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>(customer =>
            {
                customer.HasQueryFilter(row => row.TenantId == CurrentTenant);
                customer.OwnsOne(row => row.Billing);
            });
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Address Shipping { get; set; } = new();
    }

    internal sealed class Address
    {
        public string Street { get; set; } = string.Empty;
    }

    internal sealed class Customer
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public BillingAddress Billing { get; set; } = new();
    }

    internal sealed class BillingAddress
    {
        public string Street { get; set; } = string.Empty;

        public string TenantId { get; set; } = string.Empty;
    }
}
