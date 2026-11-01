// <copyright file="Tgm006Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM006 tenant-on-derived: a derived type declares the tenant property while its root has none.
internal static class Tgm006Models
{
    // Positive: a TPH hierarchy with TenantId and the filter on the root.
    internal sealed class TenantOnRoot(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Agreement>().HasQueryFilter(agreement => agreement.TenantId == CurrentTenant);
            modelBuilder.Entity<Lease>();
        }
    }

    // Negative: TenantId on the derived type only.
    internal sealed class TenantOnDerived(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>();
            modelBuilder.Entity<Invoice>();
        }
    }

    internal class Agreement
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Lease : Agreement
    {
        public int Months { get; set; }
    }

    internal class Document
    {
        public int Id { get; set; }
    }

    internal sealed class Invoice : Document
    {
        public string TenantId { get; set; } = string.Empty;
    }
}
