// <copyright file="Tgm004Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM004 literal-tenant: the tenant filter compares the tenant property to a constant.
internal static class Tgm004Models
{
    // The filter an app builds with the Expression API from its tenant field.
    private static Expression<Func<Invoice, bool>> OrElseFilter(string? tenant)
    {
        var invoice = Expression.Parameter(typeof(Invoice), "invoice");
        var body = Expression.OrElse(
            Expression.Constant(tenant == null),
            Expression.Equal(Expression.Property(invoice, nameof(Invoice.TenantId)), Expression.Constant(tenant, typeof(string))));
        return Expression.Lambda<Func<Invoice, bool>>(body, invoice);
    }

    internal static class TenantHolder
    {
        public static string Current { get; set; } = "acme";
    }

    // Positive: a static member is rooted at a static, so it is OK.
    internal sealed class StaticMember(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantHolder.Current);
    }

    // Negative: a string literal.
    internal sealed class StringLiteral(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == "acme");
    }

    // Negative: a numeric literal on an int tenant key.
    internal sealed class NumericLiteral(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Shipment>().HasQueryFilter(shipment => shipment.TenantId == 42);
    }

    // Negative: a filter built by hand from a field of the context, as false || e.TenantId == "acme"
    // for a context built as acme.
    internal sealed class OrElseBuiltAsTenant(DbContextOptions options) : DbContext(options)
    {
        private readonly string? _tenant = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(OrElseFilter(_tenant));
    }

    // Negative: the same filter built with no tenant, true || e.TenantId == null, filters nothing (TGM001).
    internal sealed class OrElseBuiltWithoutTenant(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(OrElseFilter(tenant: null));
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Shipment
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
    }
}
