// <copyright file="Tgm009Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM009 unindexed-tenant: no index or key of a tenant-scoped entity type starts with its tenant
// property. Every model filters on the tenant, so only the indexes differ.
internal static class Tgm009Models
{
    // Negative: the only key is Id.
    internal sealed class KeyOnly(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(row => row.TenantId == CurrentTenant);
    }

    // Negative: an index includes TenantId, but not first.
    internal sealed class TenantSecond(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => new { row.Number, row.TenantId });
            });
    }

    // Positive: an index on TenantId.
    internal sealed class TenantIndex(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => row.TenantId);
            });
    }

    // Positive: the primary key starts with TenantId.
    internal sealed class TenantFirstKey(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasKey(row => new { row.TenantId, row.Id });
            });
    }

    // Positive: the index EF Core adds for a foreign key of (TenantId, OrderId) starts with
    // TenantId; Order's primary key, (TenantId, Id), does too.
    internal sealed class TenantForeignKey(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(order =>
            {
                order.HasQueryFilter(row => row.TenantId == CurrentTenant);
                order.HasKey(row => new { row.TenantId, row.Id });
            });
            modelBuilder.Entity<OrderLine>(line =>
            {
                line.HasQueryFilter(row => row.TenantId == CurrentTenant);
                line.HasOne(row => row.Order).WithMany().HasForeignKey(row => new { row.TenantId, row.OrderId });
            });
        }
    }

    // Positive: in table per hierarchy, an index declared on a derived type indexes the shared table.
    internal sealed class IndexOnDerived(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment>().HasQueryFilter(row => row.TenantId == CurrentTenant);
            modelBuilder.Entity<CardPayment>().HasIndex(row => new { row.TenantId, row.Last4 });
        }
    }

    // Skipped: a view holds no index of its own.
    internal sealed class MappedToView(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.ToView("InvoiceView");
            });
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;

        public int Year { get; set; }

        public string Code { get; set; } = string.Empty;
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class OrderLine
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;
    }

    internal class Payment
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class CardPayment : Payment
    {
        public string Last4 { get; set; } = string.Empty;
    }
}
