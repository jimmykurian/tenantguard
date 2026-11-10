// <copyright file="Tgm011Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM011 tenant-free-view: an entity type mapped to a view or SQL query with no tenant property, in a
// model with a tenant-scoped entity type.
internal static class Tgm011Models
{
    // Negative: a view of totals that drops the tenant column.
    internal sealed class TenantFreeView(DbContextOptions options) : TenantData(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<InvoiceTotal>().HasNoKey().ToView("invoice_totals");
        }
    }

    // Negative: the same, from a SQL query.
    internal sealed class TenantFreeQuery(DbContextOptions options) : TenantData(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<InvoiceTotal>().HasNoKey().ToSqlQuery("SELECT \"Number\", 0 AS \"Total\" FROM \"Invoice\"");
        }
    }

    // Positive: the view selects the tenant column, and the type filters on it.
    internal sealed class TenantView(DbContextOptions options) : TenantData(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TenantTotal>(total =>
            {
                total.HasNoKey().ToView("tenant_totals");
                total.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
        }
    }

    // Positive: a model with no tenant data.
    internal sealed class NoTenantData(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<InvoiceTotal>().HasNoKey().ToView("invoice_totals");
    }

    internal abstract class TenantData(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(row => row.TenantId == CurrentTenant);
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Number { get; set; } = string.Empty;
    }

    internal sealed class InvoiceTotal
    {
        public string Number { get; set; } = string.Empty;

        public decimal Total { get; set; }
    }

    internal sealed class TenantTotal
    {
        public string TenantId { get; set; } = string.Empty;

        public decimal Total { get; set; }
    }
}
