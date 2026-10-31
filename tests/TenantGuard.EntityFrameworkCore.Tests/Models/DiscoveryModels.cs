// <copyright file="DiscoveryModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Tenant property discovery. None of these have a filter, so TGM001 names the property
// discovery picked.
internal static class DiscoveryModels
{
    // [TenantKey] on a property whose name is not a tenant property name.
    internal sealed class Attributed(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ledger>();
    }

    // Both TenantId and [TenantKey] OrgId: the attribute wins over the names.
    internal sealed class AttributeOverName(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Journal>();
    }

    // OrgId with no attribute, for explicit registration and custom names.
    internal sealed class Plain(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Account>();
    }

    internal sealed class Ledger
    {
        public int Id { get; set; }

        [TenantKey]
        public string OrgId { get; set; } = string.Empty;
    }

    internal sealed class Journal
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        [TenantKey]
        public string OrgId { get; set; } = string.Empty;
    }

    internal sealed class Account
    {
        public int Id { get; set; }

        public string OrgId { get; set; } = string.Empty;
    }
}
