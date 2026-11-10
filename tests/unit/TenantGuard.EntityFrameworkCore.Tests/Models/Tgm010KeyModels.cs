// <copyright file="Tgm010KeyModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM010 on a primary key: a natural key, whose values aren't generated, leaves out the tenant
// property. Each model also indexes TenantId, so TGM009 stays out of the way.
internal static class Tgm010KeyModels
{
    // Negative: an external login keyed by its provider and the provider's key.
    internal sealed class NaturalKey(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExternalLogin>(login =>
            {
                login.HasKey(row => new { row.LoginProvider, row.ProviderKey });
                login.HasIndex(row => row.TenantId);
                login.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
    }

    // Positive: the same key, per tenant.
    internal sealed class NaturalKeyPerTenant(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ExternalLogin>(login =>
            {
                login.HasKey(row => new { row.TenantId, row.LoginProvider, row.ProviderKey });
                login.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
    }

    // Positive: a Guid key the app assigns, with value generation off, as ABP does.
    internal sealed class AssignedGuid(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Device>(device =>
            {
                device.Property(row => row.Id).ValueGeneratedNever();
                device.HasIndex(row => row.TenantId);
                device.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
    }

    // Positive: a profile that shares its account's key, a foreign key to a tenant-scoped row.
    internal sealed class SharedPrimaryKey(DbContextOptions options) : DbContext(options)
    {
        public string CurrentTenant { get; set; } = "acme";

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(account =>
            {
                account.HasIndex(row => row.TenantId);
                account.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
            modelBuilder.Entity<Profile>(profile =>
            {
                profile.HasOne<Account>().WithOne().HasForeignKey<Profile>(row => row.AccountId);
                profile.HasKey(row => row.AccountId);
                profile.HasIndex(row => row.TenantId);
                profile.HasQueryFilter(row => row.TenantId == CurrentTenant);
            });
        }
    }

    internal sealed class ExternalLogin
    {
        public string TenantId { get; set; } = string.Empty;

        public string LoginProvider { get; set; } = string.Empty;

        public string ProviderKey { get; set; } = string.Empty;
    }

    internal sealed class Device
    {
        public Guid Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Account
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Profile
    {
        public int AccountId { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }
}
