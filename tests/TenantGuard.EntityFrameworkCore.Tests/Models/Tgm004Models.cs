// <copyright file="Tgm004Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM004 literal-tenant: the tenant filter compares the tenant property to a constant.
internal static class Tgm004Models
{
    internal static class TenantHolder
    {
        public static string Current { get; set; } = "acme";
    }

    // Positive: a static member is rooted at a static, so it is OK (SPEC 4.2).
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
