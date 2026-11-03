// <copyright file="PerTenantModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Models for AllowCapturedValues: a Ledger whose filter captures the tenant when the model is built,
// and the model cache key factories an app might use.
internal static class PerTenantModels
{
    // Builds a Ledger for a tenant on a SQLite connection, with a key factory (none keeps EF Core's
    // own) and TenantGuard configured by tenantGuard.
    public static Ledger Create(
        SqliteConnection connection,
        string? tenant,
        Type? keyFactory,
        Action<TenantGuardOptionsBuilder> tenantGuard)
    {
        var builder = new DbContextOptionsBuilder<Ledger>().UseSqlite(connection);
        if (keyFactory is not null)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(
                builder.Options.FindExtension<CoreOptionsExtension>()!.WithReplacedService(typeof(IModelCacheKeyFactory), keyFactory));
        }

        builder.UseTenantGuard(tenantGuard);
        return new Ledger(builder.Options, tenant);
    }

    // The tenant is read once, when the model is built: right only with a model per tenant.
    internal sealed class Ledger(DbContextOptions<Ledger> options, string? tenant) : DbContext(options)
    {
        public string? TenantId { get; } = tenant;

        public DbSet<Entry> Entries => Set<Entry>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var current = TenantId;
            modelBuilder.Entity<Entry>(entry =>
            {
                entry.HasQueryFilter(row => row.TenantId == current);
                entry.HasIndex(row => row.TenantId);
            });
        }
    }

    internal sealed class Entry
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    // Keys the cache by tenant, as the docs show.
    internal sealed class TenantKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((Ledger)context).TenantId, designTime);
    }

    // A tuple of its own that leaves the tenant out: TGM003 stays.
    internal sealed class VersionKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), "v2", designTime);
    }

    // A key of its own type that ignores the tenant: it passes the startup check, so every tenant
    // shares the first tenant's model, and the write guard blocks the second tenant's save.
    internal sealed class SharedKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => new SharedKey(context.GetType(), designTime);
    }

    internal sealed record SharedKey(Type Context, bool DesignTime);
}
