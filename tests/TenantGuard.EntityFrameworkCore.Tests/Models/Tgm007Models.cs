// <copyright file="Tgm007Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM007 filter-not-parameterized: the tenant filter's operand cannot be classified.
internal static class Tgm007Models
{
    // Positive: a method on the context is rooted at the context.
    internal sealed class ContextMethod(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == GetTenant());

        private string GetTenant() => CurrentTenant;
    }

    // Negative: compared to another column of the same row.
    internal sealed class OtherColumn(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == invoice.OwnerTenantId);
    }

    // Negative: a field of a configuration class, which EF does not re-read per query.
    internal sealed class ConfigurationField(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
    }

    // Negative: TenantId is referenced, but never compared to a tenant.
    internal sealed class NullCheckOnly(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId != null);
    }

    internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
    {
        private readonly string _tenant = "acme";

        public void Configure(EntityTypeBuilder<Invoice> builder)
            => builder.HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string OwnerTenantId { get; set; } = string.Empty;
    }
}
