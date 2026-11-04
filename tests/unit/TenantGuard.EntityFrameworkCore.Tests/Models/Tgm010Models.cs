// <copyright file="Tgm010Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using static TenantGuard.EntityFrameworkCore.Tests.Models.Tgm009Models;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM010 tenant-free-unique: a unique index or alternate key leaves out the tenant property. Each
// model also indexes TenantId, so TGM009 stays out of the way.
internal static class Tgm010Models
{
    // Negative: Number is unique across tenants.
    internal sealed class UniqueNumber(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => row.TenantId);
                invoice.HasIndex(row => row.Number).IsUnique();
            });
    }

    // Negative: two columns, neither the tenant.
    internal sealed class UniqueYearAndNumber(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => row.TenantId);
                invoice.HasIndex(row => new { row.Year, row.Number }).IsUnique().HasFilter("\"Number\" <> ''");
            });
    }

    // Negative: an alternate key on Code.
    internal sealed class AlternateCode(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => row.TenantId);
                invoice.HasAlternateKey(row => row.Code);
            });
    }

    // Negative: a unique index declared on a derived type.
    internal sealed class UniqueOnDerived(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Payment>(payment =>
            {
                payment.HasQueryFilter(row => row.TenantId == CurrentTenant);
                payment.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<CardPayment>().HasIndex(row => row.Last4).IsUnique();
        }
    }

    // Positive: the unique index EF Core makes for a one-to-one foreign key holds Order keys.
    internal sealed class OneToOne(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(order =>
            {
                order.HasQueryFilter(row => row.TenantId == CurrentTenant);
                order.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<Receipt>(receipt =>
            {
                receipt.HasQueryFilter(row => row.TenantId == CurrentTenant);
                receipt.HasIndex(row => row.TenantId);
                receipt.HasOne(row => row.Order).WithOne().HasForeignKey<Receipt>(row => row.OrderId);
            });
        }
    }

    internal sealed class Receipt
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;
    }

    // Positive: unique per tenant, which satisfies TGM009 too.
    internal sealed class UniquePerTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => new { row.TenantId, row.Number }).IsUnique();
                invoice.HasAlternateKey(row => new { row.TenantId, row.Code });
            });
    }

    // Positive for TGM010: the tenant is in the unique index, though not first, so only TGM009.
    internal sealed class UniqueTenantSecond(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => new { row.Number, row.TenantId }).IsUnique();
            });
    }

    // Positive: every ProjectId belongs to one tenant's Project, so (ProjectId, Name) is unique
    // within a tenant.
    internal sealed class UniqueWithinTenantScopedPrincipal(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(project =>
            {
                project.HasQueryFilter(row => row.TenantId == CurrentTenant);
                project.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<ProjectTag>(tag =>
            {
                tag.HasQueryFilter(row => row.TenantId == CurrentTenant);
                tag.HasIndex(row => row.TenantId);
                tag.HasOne<Project>().WithMany().HasForeignKey(row => row.ProjectId);
                tag.HasIndex(row => new { row.ProjectId, row.Name }).IsUnique();
                tag.HasAlternateKey(row => new { row.Name, row.ProjectId });
            });
        }
    }

    // Positive: a join table whose unique index is two foreign keys to tenant-scoped principals.
    internal sealed class JoinOfTenantScopedPrincipals(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(project =>
            {
                project.HasQueryFilter(row => row.TenantId == CurrentTenant);
                project.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<Label>(label =>
            {
                label.HasQueryFilter(row => row.TenantId == CurrentTenant);
                label.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<ProjectLabel>(link =>
            {
                link.HasQueryFilter(row => row.TenantId == CurrentTenant);
                link.HasIndex(row => row.TenantId);
                link.HasOne<Project>().WithMany().HasForeignKey(row => row.ProjectId);
                link.HasOne<Label>().WithMany().HasForeignKey(row => row.LabelId);
                link.HasIndex(row => new { row.ProjectId, row.LabelId }).IsUnique();
            });
        }
    }

    // Negative: Country is shared by every tenant, so a (CountryId, Code) pair one tenant used is
    // refused to the others.
    internal sealed class UniqueWithinSharedPrincipal(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Office>(office =>
            {
                office.HasQueryFilter(row => row.TenantId == CurrentTenant);
                office.HasIndex(row => row.TenantId);
                office.HasOne<Country>().WithMany().HasForeignKey(row => row.CountryId);
                office.HasIndex(row => new { row.CountryId, row.Code }).IsUnique();
            });
    }

    internal sealed class Project
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class ProjectTag
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int ProjectId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Label
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class ProjectLabel
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int ProjectId { get; set; }

        public int LabelId { get; set; }
    }

    internal sealed class Country
    {
        public int Id { get; set; }
    }

    internal sealed class Office
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int CountryId { get; set; }

        public string Code { get; set; } = string.Empty;
    }
}
