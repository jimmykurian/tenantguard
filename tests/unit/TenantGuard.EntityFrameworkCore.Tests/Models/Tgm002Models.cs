// <copyright file="Tgm002Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM002 unscoped-entity: with RequireAllEntitiesScoped, an entity type with no tenant property that
// is not declared shared. The test picks the positive or negative case through the options.
internal static class Tgm002Models
{
    internal sealed class WithLookup(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == CurrentTenant);
            modelBuilder.Entity<Country>();
        }
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Country
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}
