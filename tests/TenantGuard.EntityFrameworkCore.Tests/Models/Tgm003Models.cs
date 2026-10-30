// <copyright file="Tgm003Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM003 captured-value: the tenant filter reads a local captured at model build.
internal static class Tgm003Models
{
    // Positive: a context member, which EF re-reads per query.
    internal sealed class ContextMember(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == CurrentTenant);
    }

    // Negative: a local captured in OnModelCreating.
    internal sealed class CapturedLocal(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = CurrentTenant;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == tenant);
        }
    }

    // Negative: a local captured in a configuration class.
    internal sealed class CapturedInConfiguration(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new InvoiceConfiguration("acme"));
    }

    internal sealed class InvoiceConfiguration(string tenant) : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            var current = tenant;
            builder.HasQueryFilter(invoice => invoice.TenantId == current);
        }
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }
}
