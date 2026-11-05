// <copyright file="DeclarationModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Models for the cross-tenant declarations (docs/reference/options.md#cross-tenant-data).
internal static class DeclarationModels
{
    // A context on a SQLite connection, with TenantGuard configured by tenantGuard after CurrentTenant
    // reads the context's Tenant, and a model cache key factory when keyFactory is set. Unless keyFactory
    // is set, EF Core's service provider cache is off: each context builds a model of its own, and the
    // many option combinations here don't add up to EF Core's ManyServiceProvidersCreatedWarning.
    public static TContext Create<TContext>(
        SqliteConnection connection,
        string? tenant,
        Action<TenantGuardOptionsBuilder> tenantGuard,
        Type? keyFactory = null,
        ILoggerFactory? loggerFactory = null)
        where TContext : TenantContext
    {
        var builder = new DbContextOptionsBuilder().UseSqlite(connection).EnableServiceProviderCaching(keyFactory is not null);
        if (loggerFactory is not null)
        {
            builder.UseLoggerFactory(loggerFactory);
        }

        if (keyFactory is not null)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
                builder.Options.FindExtension<CoreOptionsExtension>()!.WithReplacedService(typeof(IModelCacheKeyFactory), keyFactory));
        }

        builder.UseTenantGuard(tg => tenantGuard(tg.CurrentTenant(context => ((TenantContext)context).Tenant)));
        var context = (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
        context.Tenant = tenant;
        return context;
    }

    internal abstract class TenantContext(DbContextOptions options) : DbContext(options)
    {
        public string? Tenant { get; set; }
    }

    // An Invoice filtered by tenant, and a Membership and an outbox with a tenant column and no filter;
    // the outbox has no tenant index (TGM009).
    internal sealed class Members(DbContextOptions options) : TenantContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();

        public DbSet<Membership> Memberships => Set<Membership>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == Tenant);
                invoice.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<Membership>().HasIndex(row => row.TenantId);
            modelBuilder.Entity<OutboxMessage>();
        }
    }

    // A lookup table, an owned type and a hierarchy, to declare Unfiltered wrongly.
    internal sealed class Catalog(DbContextOptions options) : TenantContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Country>();
            modelBuilder.Entity<Order>(order =>
            {
                order.HasQueryFilter(row => row.TenantId == Tenant);
                order.HasIndex(row => row.TenantId);
                order.OwnsOne(row => row.Shipping);
            });
            modelBuilder.Entity<Document>(document =>
            {
                document.HasQueryFilter(row => row.TenantId == Tenant);
                document.HasIndex(row => row.TenantId);
            });
            modelBuilder.Entity<Contract>();
        }
    }

    // Invoices whose null tenant is host data, as in ABP: the host, with no tenant, reads its own rows.
    internal sealed class HostShop(DbContextOptions options) : TenantContext(options)
    {
        public DbSet<HostInvoice> Invoices => Set<HostInvoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<HostInvoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == Tenant);
                invoice.HasIndex(row => row.TenantId);
            });
    }

    // The table of tenants, filtered on its own key.
    internal sealed class Registry(DbContextOptions options) : TenantContext(options)
    {
        public DbSet<Club> Clubs => Set<Club>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Club>().HasQueryFilter(club => club.Id == Tenant);
    }

    // A model per tenant whose no-tenant model has no filter, for background work across tenants.
    internal sealed class OpenLedger(DbContextOptions options) : TenantContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var tenant = Tenant;
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasIndex(row => row.TenantId);
                if (tenant is not null)
                {
                    invoice.HasQueryFilter(row => row.TenantId == tenant);
                }
            });
        }
    }

    // Keys the model cache by the context's tenant, null included.
    internal sealed class TenantKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((TenantContext)context).Tenant, designTime);
    }

    // A key of its own that ignores the tenant, so every context gets the first context's model.
    internal sealed class SharedKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), "one model", designTime);
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Membership
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }

    internal sealed class OutboxMessage
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class HostInvoice
    {
        public int Id { get; set; }

        public string? TenantId { get; set; }
    }

    internal sealed class Club
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    internal sealed class Country
    {
        public int Id { get; set; }
    }

    internal sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Address Shipping { get; set; } = new();
    }

    internal sealed class Address
    {
        public string TenantId { get; set; } = string.Empty;
    }

    internal class Document
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Contract : Document
    {
    }
}
