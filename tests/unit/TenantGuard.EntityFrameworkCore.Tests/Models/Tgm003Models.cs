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

    // Positive: Finbuckle's pattern. The filter reads a captured null of an interface the context
    // implements, which EF replaces with the current context in every query.
    internal sealed class ContextInterface(DbContextOptions options) : DbContext(options), ITenantSource
    {
        public string? TenantId { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ITenantSource? context = null;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == context!.TenantId);
        }
    }

    // Negative: a captured tenant source the context does not implement, frozen at model build.
    internal sealed class CapturedSource(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
#pragma warning disable CA1859 // Typed as the interface on purpose, the way a tenant service from DI is.
            ITenantSource source = new FixedSource("acme");
#pragma warning restore CA1859
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == source.TenantId);
        }
    }

    internal sealed class InvoiceConfiguration(string tenant) : IEntityTypeConfiguration<Invoice>
    {
        public void Configure(EntityTypeBuilder<Invoice> builder)
        {
            var current = tenant;
            builder.HasQueryFilter(invoice => invoice.TenantId == current);
        }
    }

    internal sealed class FixedSource(string tenant) : ITenantSource
    {
        public string? TenantId { get; } = tenant;
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }
}
