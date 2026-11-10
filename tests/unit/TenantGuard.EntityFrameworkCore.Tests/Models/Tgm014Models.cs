// <copyright file="Tgm014Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM014 unscoped-dependent: an entity type with no tenant property and no filter that reaches one,
// with a required foreign key to a tenant-scoped entity type.
internal static class Tgm014Models
{
    // Negative: order lines reached only through their order.
    internal sealed class Unfiltered(DbContextOptions options) : Orders(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>().Ignore(row => row.Tags);
            modelBuilder.Entity<OrderLine>().HasOne(row => row.Order).WithMany(order => order.Lines).HasForeignKey(row => row.OrderId);
        }
    }

    // Positive: filtered through the order.
    internal sealed class ThroughPrincipal(DbContextOptions options) : Orders(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>().Ignore(row => row.Tags);
            modelBuilder.Entity<OrderLine>(line =>
            {
                line.HasOne(row => row.Order).WithMany(order => order.Lines).HasForeignKey(row => row.OrderId);
                line.HasQueryFilter(row => row.Order.TenantId == CurrentTenant);
            });
        }
    }

    // Positive: the foreign key is optional.
    internal sealed class Optional(DbContextOptions options) : Orders(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>(order =>
            {
                order.Ignore(row => row.Lines);
                order.Ignore(row => row.Tags);
            });
            modelBuilder.Entity<Coupon>().HasOne<Order>().WithMany().HasForeignKey(row => row.OrderId);
        }
    }

    // Positive: EF Core's join entity type for a many-to-many relationship of tenant-scoped types.
    internal sealed class ManyToMany(DbContextOptions options) : Orders(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>().Ignore(row => row.Lines);
            modelBuilder.Entity<Tag>().HasQueryFilter(row => row.TenantId == CurrentTenant);
            modelBuilder.Entity<Order>().HasMany(order => order.Tags).WithMany(tag => tag.Orders);
        }
    }

    internal abstract class Orders(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().HasQueryFilter(row => row.TenantId == CurrentTenant);
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public List<OrderLine> Lines { get; set; } = [];

        public List<Tag> Tags { get; set; } = [];
    }

    internal sealed class OrderLine
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;
    }

    internal sealed class Coupon
    {
        public int Id { get; set; }

        public int? OrderId { get; set; }
    }

    internal sealed class Tag
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public List<Order> Orders { get; set; } = [];
    }
}
