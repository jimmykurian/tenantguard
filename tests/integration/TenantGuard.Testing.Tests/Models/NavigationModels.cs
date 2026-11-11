// <copyright file="NavigationModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Testing.Tests.Models;

// Models for the NavigationLeak check. Every context takes its current tenant in the constructor.
internal static class NavigationModels
{
    // Basket, Line, Parcel and Tag are tenant-scoped; Region is shared. Basket has Lines (to
    // dependents), Line has Basket (to the principal), Basket and Tag are many-to-many through a join
    // type EF Core makes, and Parcel's foreign key to Basket carries the tenant. Basket also has a
    // Region, which isn't tenant-scoped, and an owned Address: neither is checked.
    internal abstract class StoreBase(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected virtual bool FiltersLines => true;

        protected virtual bool FiltersTags => true;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Region>();
            modelBuilder.Entity<Basket>(basket =>
            {
                basket.HasAlternateKey(row => new { row.TenantId, row.Id });
                basket.HasOne(row => row.Region).WithMany().HasForeignKey(row => row.RegionId);
                basket.OwnsOne(row => row.Delivery);
                basket.HasMany(row => row.Tags).WithMany(tag => tag.Baskets);
                basket.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Line>(line =>
            {
                line.HasOne(row => row.Basket).WithMany(basket => basket.Lines).HasForeignKey(row => row.BasketId);
                if (FiltersLines)
                {
                    line.HasQueryFilter(row => row.TenantId == CurrentTenant);
                }
            });
            modelBuilder.Entity<Parcel>(parcel =>
            {
                parcel.HasOne(row => row.Basket).WithMany(basket => basket.Parcels)
                    .HasForeignKey(row => new { row.TenantId, row.BasketId })
                    .HasPrincipalKey(basket => new { basket.TenantId, basket.Id });
                parcel.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Tag>(tag =>
            {
                if (FiltersTags)
                {
                    tag.HasQueryFilter(row => row.TenantId == CurrentTenant);
                }
            });
        }
    }

    internal sealed class Store(DbContextOptions options, string? tenant) : StoreBase(options, tenant);

    internal sealed class StoreWithUnfilteredLines(DbContextOptions options, string? tenant) : StoreBase(options, tenant)
    {
        protected override bool FiltersLines => false;
    }

    internal sealed class StoreWithUnfilteredTags(DbContextOptions options, string? tenant) : StoreBase(options, tenant)
    {
        protected override bool FiltersTags => false;
    }

    // Account and Profile are one to one: each tenant's seeded Profile already belongs to its own
    // Account, so neither way can a second Profile link across tenants.
    internal sealed class Members(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(account =>
            {
                account.HasOne(row => row.Profile).WithOne(profile => profile.Account).HasForeignKey<Profile>(profile => profile.AccountId);
                account.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Profile>().HasQueryFilter(row => row.TenantId == CurrentTenant);
        }
    }

    // The same one to one, optional: each seeded profile leaves AccountId null, so the unique index
    // has room for the link, and both navigations are checked.
    internal sealed class OptionalMembers(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OptionalAccount>(account =>
            {
                account.HasOne(row => row.Profile).WithOne(profile => profile.Account).HasForeignKey<OptionalProfile>(profile => profile.AccountId).IsRequired(false);
                account.HasQueryFilter(row => row.TenantId == CurrentTenant);
                account.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<OptionalProfile>(profile =>
            {
                profile.HasQueryFilter(row => row.TenantId == CurrentTenant);
                profile.HasIndex(row => row.TenantId);
            });
        }
    }

    // The same one to one, keyed by byte[]: a key TGM010 reports, so the context runs under Log.
    internal sealed class BinaryMembers(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? CurrentTenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BinaryAccount>(account =>
            {
                account.Property(row => row.Id).ValueGeneratedNever();
                account.HasOne(row => row.Profile).WithOne(profile => profile.Account).HasForeignKey<BinaryProfile>(profile => profile.AccountId);
                account.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<BinaryProfile>().HasQueryFilter(row => row.TenantId == CurrentTenant);
        }
    }

    internal sealed class Region
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Basket
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int RegionId { get; set; }

        public Region Region { get; set; } = null!;

        public Address Delivery { get; set; } = null!;

        public List<Line> Lines { get; } = [];

        public List<Parcel> Parcels { get; } = [];

        public List<Tag> Tags { get; } = [];
    }

    internal sealed class Address
    {
        public string Street { get; set; } = null!;
    }

    internal sealed class Line
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int BasketId { get; set; }

        public Basket Basket { get; set; } = null!;
    }

    internal sealed class Parcel
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int BasketId { get; set; }

        public Basket Basket { get; set; } = null!;
    }

    internal sealed class Tag
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public List<Basket> Baskets { get; } = [];
    }

    internal sealed class Account
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Profile? Profile { get; set; }
    }

    internal sealed class BinaryAccount
    {
        public byte[] Id { get; set; } = [];

        public string TenantId { get; set; } = string.Empty;

        public BinaryProfile? Profile { get; set; }
    }

    internal sealed class BinaryProfile
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public byte[] AccountId { get; set; } = [];

        public BinaryAccount Account { get; set; } = null!;
    }

    internal sealed class OptionalAccount
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public OptionalProfile? Profile { get; set; }
    }

    internal sealed class OptionalProfile
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int? AccountId { get; set; }

        public OptionalAccount? Account { get; set; }
    }

    internal sealed class Profile
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int AccountId { get; set; }

        public Account Account { get; set; } = null!;
    }
}
