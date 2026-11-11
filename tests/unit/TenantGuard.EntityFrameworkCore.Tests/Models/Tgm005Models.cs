// <copyright file="Tgm005Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM005 nullable-tenant: the tenant property is nullable.
internal static class Tgm005Models
{
    // Positive: a required TenantId.
    internal sealed class RequiredTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == CurrentTenant);
    }

    // Negative: a nullable TenantId.
    internal sealed class NullableTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Receipt>().HasQueryFilter(receipt => receipt.TenantId == CurrentTenant);
    }

    // Negative: rows with no tenant are ORed in, so every tenant reads them.
    internal sealed class SharedWithEveryTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Receipt>().HasQueryFilter(receipt => receipt.TenantId == CurrentTenant || receipt.TenantId == null);
            modelBuilder.Entity<Receipt>().HasIndex(receipt => receipt.TenantId);
        }
    }

    // Negative: the same, as !(TenantId != tenant && TenantId != null).
    internal sealed class SharedWithEveryTenantNegated(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Receipt>().HasQueryFilter(receipt => !(receipt.TenantId != CurrentTenant && receipt.TenantId != null));
            modelBuilder.Entity<Receipt>().HasIndex(receipt => receipt.TenantId);
        }
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Receipt
    {
        public int Id { get; set; }

        public string? TenantId { get; set; }
    }
}
