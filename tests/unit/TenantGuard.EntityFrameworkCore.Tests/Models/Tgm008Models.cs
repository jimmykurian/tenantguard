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

    // Positive: anything owned by an entity named Tenant gets EF Core's shadow ownership key
    // TenantId, which is the key to the owner, not a tenant property.
    internal sealed class OwnedByTenantRegistry(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Tenant>(tenant =>
            {
                tenant.OwnsOne(row => row.Audit);
                tenant.Ignore(row => row.Branding);
            });
    }

    // Positive: the owned type declares TenantId itself, and EF Core uses it as the ownership key.
    internal sealed class OwnedWithClrOwnershipKey(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Tenant>(tenant =>
            {
                tenant.OwnsOne(row => row.Branding);
                tenant.Ignore(row => row.Audit);
            });
    }

    // Positive: with TenantPropertyNames("ClubId"), the types Club owns get a shadow ClubId.
    internal sealed class OwnedByRenamedTenantRegistry(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Club>(club =>
            {
                club.OwnsOne(row => row.Hours);
                club.OwnsMany(row => row.PrimeTimes);
            });
    }

    // Negative: an owned type with its own TenantId beside the ownership key to its owner.
    internal sealed class OwnedByTenantRegistryWithTenant(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Workspace>().OwnsOne(row => row.Billing);
    }

    internal sealed class Tenant
    {
        public Guid Id { get; set; }

        public Stamp Audit { get; set; } = new();

        public Branding Branding { get; set; } = new();
    }

    internal sealed class Stamp
    {
        public DateTime At { get; set; }
    }

    internal sealed class Branding
    {
        public Guid TenantId { get; set; }

        public string Color { get; set; } = string.Empty;
    }

    internal sealed class Club
    {
        public int Id { get; set; }

        public OpeningHours Hours { get; set; } = new();

        public List<PrimeTime> PrimeTimes { get; set; } = [];
    }

    internal sealed class OpeningHours
    {
        public int Opens { get; set; }
    }

    internal sealed class PrimeTime
    {
        public int Starts { get; set; }
    }

    internal sealed class Workspace
    {
        public int Id { get; set; }

        public BillingAddress Billing { get; set; } = new();
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
