// <copyright file="Tgm001Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM001 unguarded-entity: a tenant-scoped entity whose filters never reference the tenant property.
internal static class Tgm001Models
{
    // Positive: the filter compares TenantId to a context member.
    internal sealed class Guarded(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == CurrentTenant);
    }

    // Negative: TenantId and no filter at all.
    internal sealed class Unguarded(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>();
    }

    // Positive: a named tenant filter next to a named soft-delete filter (EF 10 named filters).
    internal sealed class NamedFilters(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>()
                .HasQueryFilter("TenantFilter", invoice => invoice.TenantId == CurrentTenant)
                .HasQueryFilter("SoftDeleteFilter", invoice => !invoice.IsDeleted);
    }

    // Negative: a filter that does not touch TenantId.
    internal sealed class SoftDeleteOnly(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => !invoice.IsDeleted);
    }

    // Negative: keyless entity types are examined like any other.
    internal sealed class KeylessUnguarded(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<InvoiceTotal>().HasNoKey();
    }

    // Positive: a keyless entity type with a tenant filter.
    internal sealed class KeylessGuarded(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<InvoiceTotal>().HasNoKey().HasQueryFilter(total => total.TenantId == CurrentTenant);
    }

    // Negative: a shadow TenantId and no filter. Shadow strings are nullable unless marked required.
    internal sealed class ShadowUnguarded(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Note>().Property<string>("TenantId").IsRequired();
    }

    // Positive: a shadow TenantId filtered through EF.Property.
    internal sealed class ShadowGuarded(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Note>(note =>
            {
                note.Property<string>("TenantId").IsRequired();
                note.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
            });
    }

    // Positive: EF.Property with the tenant property's name in a captured local.
    internal sealed class CapturedPropertyName(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var name = nameof(Invoice.TenantId);
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => EF.Property<string>(invoice, name) == CurrentTenant);
        }
    }

    // Positive: EF.Property<Guid> with a captured name, compared lifted to a Guid? context member.
    internal sealed class CapturedPropertyNameLifted(DbContextOptions options) : DbContext(options)
    {
        public Guid? CurrentTenantId { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var name = nameof(Ledger.TenantId);
            modelBuilder.Entity<Ledger>().HasQueryFilter(ledger => EF.Property<Guid>(ledger, name) == CurrentTenantId);
        }
    }

    // Positive: a generic helper on a base context builds the filter from a name parameter.
    internal sealed class CapturedPropertyNameFromHelper(DbContextOptions options) : FilteringContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => FilterByTenant<Invoice>(modelBuilder, nameof(Invoice.TenantId));
    }

    // Negative: EF.Property with a captured name that isn't the tenant property.
    internal sealed class CapturedOtherPropertyName(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var name = nameof(Invoice.IsDeleted);
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => !EF.Property<bool>(invoice, name));
        }
    }

    internal abstract class FilteringContext(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected void FilterByTenant<TEntity>(ModelBuilder modelBuilder, string property)
            where TEntity : class
            => modelBuilder.Entity<TEntity>().HasQueryFilter(row => EF.Property<string>(row, property) == CurrentTenant);
    }

    // Positive: Post is scoped through its principal, p => p.Blog.TenantId == ...
    internal sealed class ThroughNavigation(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().HasQueryFilter(blog => blog.TenantId == CurrentTenant);
            modelBuilder.Entity<Post>().HasQueryFilter(post => post.Blog.TenantId == CurrentTenant);
        }
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
    }

    internal sealed class Ledger
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }
    }

    internal sealed class InvoiceTotal
    {
        public string TenantId { get; set; } = string.Empty;

        public decimal Total { get; set; }
    }

    internal sealed class Note
    {
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    internal sealed class Blog
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Post
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int BlogId { get; set; }

        public Blog Blog { get; set; } = null!;
    }
}
