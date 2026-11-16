// <copyright file="Tgm015Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM015 tenant-free-foreign-key: a foreign key between two tenant-scoped entity types that doesn't
// pair their tenant properties.
internal static class Tgm015Models
{
    internal static class Retail
    {
        internal sealed class Order : Sale
        {
            public int? RetailCustomerId { get; set; }
        }
    }

    internal static class Wholesale
    {
        internal sealed class Order : Sale
        {
            public int? WholesaleCustomerId { get; set; }
        }
    }

    internal abstract class Billing(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(customer =>
            {
                customer.HasQueryFilter(row => row.TenantId == CurrentTenant);
                customer.HasIndex(row => row.TenantId);
                customer.Ignore(row => row.Tags);
            });
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == CurrentTenant);
                invoice.HasIndex(row => row.TenantId);
                invoice.Ignore(row => row.BillTo);
                invoice.Ignore(row => row.Lines);
            });
        }
    }

    // Negative: CustomerId alone.
    internal sealed class SingleColumn(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId).IsRequired();
        }
    }

    // Negative: the same foreign key, optional.
    internal sealed class Optional(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId);
        }
    }

    // Positive: the tenant pair is in the foreign key.
    internal sealed class Composite(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().HasOne(row => row.Customer).WithMany()
                .HasForeignKey(row => new { row.TenantId, row.CustomerId })
                .HasPrincipalKey(row => new { row.TenantId, row.Id });
        }
    }

    // Negative: the principal's tenant property is in the key, paired with a column that isn't the
    // invoice's tenant property.
    internal sealed class OtherTenantColumn(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().HasOne(row => row.Customer).WithMany()
                .HasForeignKey(row => new { row.CustomerTenantId, row.CustomerId })
                .HasPrincipalKey(row => new { row.TenantId, row.Id });
        }
    }

    // Negative: the invoice's tenant property is the foreign key, paired with the customer's Code.
    internal sealed class Mispaired(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.Ignore(row => row.Customer);
                invoice.HasOne<Customer>().WithMany().HasForeignKey(row => row.TenantId).HasPrincipalKey(row => row.Code);
            });
        }
    }

    // Negative: two foreign keys, reported in the order of their columns.
    internal sealed class TwoForeignKeys(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId);
                invoice.HasOne<Customer>().WithMany().HasForeignKey(row => row.BillToId);
            });
        }
    }

    // Negative: a self-reference.
    internal sealed class SelfReference(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Category>(category =>
            {
                category.HasQueryFilter(row => row.TenantId == CurrentTenant);
                category.HasIndex(row => row.TenantId);
                category.HasOne(row => row.Parent).WithMany().HasForeignKey(row => row.ParentId);
            });
    }

    // Negative: the foreign key is declared on a derived type.
    internal sealed class OnDerived(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().Ignore(row => row.Customer);
            modelBuilder.Entity<CreditNote>().HasOne<Customer>().WithMany().HasForeignKey(row => row.RefundedCustomerId);
        }
    }

    // Negative: shadow tenant properties, as Finbuckle's IsMultiTenant() adds.
    internal sealed class ShadowTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(project =>
            {
                project.Property<string>("TenantId").IsRequired();
                project.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
                project.HasIndex("TenantId");
            });
            modelBuilder.Entity<ProjectTask>(task =>
            {
                task.Property<string>("TenantId").IsRequired();
                task.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
                task.HasIndex("TenantId");
                task.HasOne<Project>().WithMany().HasForeignKey(row => row.ProjectId);
            });
        }
    }

    // Positive: the same model with the foreign key and principal key given by name.
    internal sealed class ShadowTenantComposite(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Project>(project =>
            {
                project.Property<string>("TenantId").IsRequired();
                project.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
            });
            modelBuilder.Entity<ProjectTask>(task =>
            {
                task.Property<string>("TenantId").IsRequired();
                task.HasQueryFilter(row => EF.Property<string>(row, "TenantId") == CurrentTenant);
                task.HasOne<Project>().WithMany().HasForeignKey("TenantId", nameof(ProjectTask.ProjectId)).HasPrincipalKey("TenantId", nameof(Project.Id));
            });
        }
    }

    // Positive: the principal has no tenant property.
    internal sealed class SharedPrincipal(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.Ignore(row => row.Customer);
                invoice.HasOne<Country>().WithMany().HasForeignKey(row => row.CountryId);
            });
        }
    }

    // Positive: the table of tenants, tenant-scoped on its key, which the member's tenant property points at.
    internal sealed class TableOfTenants(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Club>().HasQueryFilter(row => row.Id == CurrentTenant);
            modelBuilder.Entity<Member>(member =>
            {
                member.HasQueryFilter(row => row.TenantId == CurrentTenant);
                member.HasOne<Club>().WithMany().HasForeignKey(row => row.TenantId);
            });
        }
    }

    // Negative: a second foreign key to the table of tenants, which can name another tenant.
    internal sealed class SecondClub(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Club>().HasQueryFilter(row => row.Id == CurrentTenant);
            modelBuilder.Entity<Member>(member =>
            {
                member.HasQueryFilter(row => row.TenantId == CurrentTenant);
                member.HasOne<Club>().WithMany().HasForeignKey(row => row.TenantId);
                member.HasOne<Club>().WithMany().HasForeignKey(row => row.HomeClubId);
            });
        }
    }

    // Negative: two derived types with the same display name, each with a foreign key of its own.
    internal sealed class SameNameSiblings(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().Ignore(row => row.Customer);
            modelBuilder.Entity<Sale>(sale =>
            {
                sale.HasQueryFilter(row => row.TenantId == CurrentTenant);
                sale.HasIndex(row => row.TenantId);
                sale.HasDiscriminator<string>("Kind").HasValue<Sale>("sale").HasValue<Retail.Order>("retail").HasValue<Wholesale.Order>("wholesale");
            });
            modelBuilder.Entity<Retail.Order>().HasOne<Customer>().WithMany().HasForeignKey(row => row.RetailCustomerId);
            modelBuilder.Entity<Wholesale.Order>().HasOne<Customer>().WithMany().HasForeignKey(row => row.WholesaleCustomerId);
        }
    }

    // Positive: the dependent has no tenant property; TGM014 is the rule for it.
    internal sealed class UnscopedDependent(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>().Ignore(row => row.Customer);
            modelBuilder.Entity<Note>().HasOne<Customer>().WithMany().HasForeignKey(row => row.CustomerId).IsRequired();
        }
    }

    // Positive: an owned type's rows are its owner's.
    internal sealed class OwnedDependent(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.Ignore(row => row.Customer);
                invoice.OwnsOne(row => row.BillTo);
                invoice.OwnsMany(row => row.Lines);
            });
        }
    }

    // Positive: EF Core's own join entity type has no tenant column.
    internal sealed class ImplicitJoin(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(customer =>
            {
                customer.HasQueryFilter(row => row.TenantId == CurrentTenant);
                customer.HasIndex(row => row.TenantId);
                customer.HasMany(row => row.Tags).WithMany();
            });
            modelBuilder.Entity<Tag>(tag =>
            {
                tag.HasQueryFilter(row => row.TenantId == CurrentTenant);
                tag.HasIndex(row => row.TenantId);
            });
        }
    }

    // Negative: a join entity type with a tenant property has its foreign keys checked.
    internal sealed class ExplicitJoin(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>(customer =>
            {
                customer.HasQueryFilter(row => row.TenantId == CurrentTenant);
                customer.HasIndex(row => row.TenantId);
                customer.HasMany(row => row.Tags).WithMany().UsingEntity<CustomerTag>(
                    join => join.HasOne<Tag>().WithMany().HasForeignKey(row => row.TagId),
                    join => join.HasOne<Customer>().WithMany().HasForeignKey(row => row.CustomerId),
                    join =>
                    {
                        join.HasKey(row => new { row.TenantId, row.CustomerId, row.TagId });
                        join.HasQueryFilter(row => row.TenantId == CurrentTenant);
                    });
            });
            modelBuilder.Entity<Tag>(tag =>
            {
                tag.HasQueryFilter(row => row.TenantId == CurrentTenant);
                tag.HasIndex(row => row.TenantId);
            });
        }
    }

    // Positive: the principal is mapped to a view, so the database holds no constraint.
    internal sealed class PrincipalIsView(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Customer>().ToView("CustomerView");
            modelBuilder.Entity<Invoice>().HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId);
        }
    }

    // Positive: the dependent is mapped to a view.
    internal sealed class DependentIsView(DbContextOptions options) : Billing(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.ToView("InvoiceView");
                invoice.HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId);
            });
        }
    }

    internal sealed class Customer
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public List<Tag> Tags { get; } = [];
    }

    internal class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string? CustomerTenantId { get; set; }

        public int? CustomerId { get; set; }

        public int? BillToId { get; set; }

        public int? CountryId { get; set; }

        public Customer? Customer { get; set; }

        public Address? BillTo { get; set; }

        public List<Line> Lines { get; } = [];
    }

    internal sealed class CreditNote : Invoice
    {
        public int? RefundedCustomerId { get; set; }
    }

    internal sealed class Address
    {
        public string Street { get; set; } = string.Empty;
    }

    internal sealed class Line
    {
        public int Id { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    internal sealed class Country
    {
        public int Id { get; set; }
    }

    internal sealed class Note
    {
        public int Id { get; set; }

        public int CustomerId { get; set; }
    }

    internal sealed class Category
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int? ParentId { get; set; }

        public Category? Parent { get; set; }
    }

    internal sealed class Project
    {
        public int Id { get; set; }
    }

    internal sealed class ProjectTask
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
    }

    internal sealed class Club
    {
        public string Id { get; set; } = string.Empty;
    }

    internal sealed class Member
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string? HomeClubId { get; set; }
    }

    internal class Sale
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Tag
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class CustomerTag
    {
        public string TenantId { get; set; } = string.Empty;

        public int CustomerId { get; set; }

        public int TagId { get; set; }
    }
}
