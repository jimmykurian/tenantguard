// <copyright file="ProbeModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Testing.Tests.Models;

// Models for the probe. Every context takes its current tenant in the constructor.
internal static class ProbeModels
{
    internal enum InvoiceStatus
    {
        Draft,
        Sent,
    }

    // Customer, Invoice, Note and Order are tenant-scoped; Country is shared. Customer requires a
    // Country, Invoice requires a Customer, Note has a shadow TenantId and a key EF does not generate,
    // and Order requires its owned Address.
    internal abstract class ShopBase(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        public DbSet<Country> Countries => Set<Country>();

        // A leaky model leaves Customer without its filter.
        protected virtual bool FiltersCustomers => true;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Country>();
            modelBuilder.Entity<Customer>(customer =>
            {
                customer.HasOne<Country>().WithMany().HasForeignKey(row => row.CountryId);
                if (FiltersCustomers)
                {
                    customer.HasQueryFilter(row => row.TenantId == CurrentTenant);
                }
            });
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasOne<Customer>().WithMany().HasForeignKey(row => row.CustomerId);
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Note>(note =>
            {
                note.HasKey(row => row.Code);
                note.Property<string>("TenantId").IsRequired();
                note.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
            });
            modelBuilder.Entity<Order>(order =>
            {
                order.OwnsOne(row => row.Shipping);
                order.Navigation(row => row.Shipping).IsRequired();
                order.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
        }
    }

    internal sealed class Shop(DbContextOptions options, string? tenant) : ShopBase(options, tenant);

    internal sealed class LeakyShop(DbContextOptions options, string? tenant) : ShopBase(options, tenant)
    {
        protected override bool FiltersCustomers => false;
    }

    // CycleA and CycleB require each other; Plain needs nothing.
    internal sealed class CycleShop(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CycleA>(a =>
            {
                a.HasOne<CycleB>().WithMany().HasForeignKey(row => row.CycleBId).OnDelete(DeleteBehavior.Restrict);
                a.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<CycleB>(b =>
            {
                b.HasOne<CycleA>().WithMany().HasForeignKey(row => row.CycleAId).OnDelete(DeleteBehavior.Restrict);
                b.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Plain>().HasQueryFilter(row => row.TenantId == CurrentTenant);
        }
    }

    // Subscription has only a constructor with a parameter; Seat requires a Subscription.
    internal sealed class SubscriptionShop(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Subscription>().HasQueryFilter(row => row.TenantId == CurrentTenant);
            modelBuilder.Entity<Seat>(seat =>
            {
                seat.HasOne<Subscription>().WithMany().HasForeignKey(row => row.SubscriptionId);
                seat.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
        }
    }

    // The tenant is a Guid.
    internal sealed class GuidShop(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public Guid? CurrentTenant { get; } = Guid.TryParse(tenant, out var id) ? id : null;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>().HasQueryFilter(row => row.TenantId == CurrentTenant);
    }

    // Nothing is tenant-scoped.
    internal sealed class LookupShop(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Country>();
    }

    internal sealed class Country
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Customer
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public int CountryId { get; set; }
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public DateOnly IssuedOn { get; set; }

        public DateTime CreatedAt { get; set; }

        public Guid Reference { get; set; }

        public bool Paid { get; set; }

        public InvoiceStatus Status { get; set; }

        public string? Memo { get; set; }

        public int CustomerId { get; set; }
    }

    internal sealed class Note
    {
        public string Code { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Address Shipping { get; set; } = null!;
    }

    internal sealed class Address
    {
        public string Street { get; set; } = null!;

        public string City { get; set; } = null!;
    }

    internal sealed class CycleA
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int CycleBId { get; set; }
    }

    internal sealed class CycleB
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int CycleAId { get; set; }
    }

    internal sealed class Plain
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Subscription(string plan)
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Plan { get; set; } = plan;
    }

    internal sealed class Seat
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int SubscriptionId { get; set; }
    }

    internal sealed class Ledger
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
