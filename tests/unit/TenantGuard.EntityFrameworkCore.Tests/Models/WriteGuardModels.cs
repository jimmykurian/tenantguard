// <copyright file="WriteGuardModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Models for the write guard.
internal static class WriteGuardModels
{
    // A tenant-scoped Invoice, a Note with a shadow TenantId, an Order that owns an Address (with a
    // TenantId of its own, which the guard ignores, and a nested GeoPoint), OrderLines keyed by the order
    // and Parcels with a key of their own, which can move to another order, and a shared Country.
    internal sealed class Shop(DbContextOptions options) : DbContext(options)
    {
        public string? CurrentTenant { get; set; }

        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<Note> Notes => Set<Note>();

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<Country> Countries => Set<Country>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == CurrentTenant);
            modelBuilder.Entity<Note>(note =>
            {
                note.Property<string>("TenantId").IsRequired();
                note.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
            });
            modelBuilder.Entity<Order>(order =>
            {
                order.HasQueryFilter(row => row.TenantId == CurrentTenant);
                order.OwnsOne(row => row.Shipping, shipping => shipping.OwnsOne(address => address.Geo));
                order.OwnsMany(row => row.Lines);
                order.OwnsMany(row => row.Parcels, parcel =>
                {
                    parcel.HasKey(row => row.Id);
                    parcel.Property(row => row.Id).ValueGeneratedNever();
                });
            });
            modelBuilder.Entity<Country>();
        }
    }

    // No tenant-scoped entity types at all.
    internal sealed class LookupsOnly(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Country> Countries => Set<Country>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Country>();
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;
    }

    internal sealed class Note
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Address Shipping { get; set; } = new();

        public List<OrderLine> Lines { get; set; } = [];

        public List<Parcel> Parcels { get; set; } = [];
    }

    internal sealed class Address
    {
        public string Street { get; set; } = string.Empty;

        public string TenantId { get; set; } = string.Empty;

        public GeoPoint Geo { get; set; } = new();
    }

    internal sealed class GeoPoint
    {
        public double Latitude { get; set; }
    }

    internal sealed class OrderLine
    {
        public int Id { get; set; }

        public string Sku { get; set; } = string.Empty;
    }

    internal sealed class Parcel
    {
        public int Id { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    internal sealed class Country
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
