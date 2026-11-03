// <copyright file="PerTenantModelProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard.Testing.Tests;

// The probe on a model per tenant, under AllowCapturedValues: with a cache key that includes the
// tenant every check passes; with one that ignores it, the second tenant gets the first's model, and
// the write guard stops the probe's seeding.
public sealed class PerTenantModelProbeTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Every_check_passes_with_a_model_per_tenant()
    {
        var report = await ProbeAsync<TenantKeyFactory>();

        report.Rows.Should().NotBeEmpty().And.OnlyContain(row => row.Outcome == ProbeOutcome.Passed, string.Join("; ", report.Rows.Select(row => $"{row.Check}: {row.Detail}")));
    }

    [Fact]
    public async Task A_cache_key_that_ignores_the_tenant_stops_the_probe()
    {
        var exception = (await FluentActions.Awaiting(ProbeAsync<SharedKeyFactory>).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be(
            "The write guard blocked seeding Entry as globex, because the context's model was built for another tenant. "
            + "Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.");
        exception.InnerException.Should().BeOfType<TenantIsolationViolationException>();
    }

    private async Task<ProbeReport> ProbeAsync<TKeyFactory>()
        where TKeyFactory : IModelCacheKeyFactory
    {
        await _connection.OpenAsync(Token);
        var options = new DbContextOptionsBuilder<Ledger>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCacheKeyFactory, TKeyFactory>()
            .UseTenantGuard(tg => tg.AllowCapturedValues().CurrentTenant(context => ((Ledger)context).Tenant))
            .Options;
        await using (var setup = new Ledger(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(Token);
        }

        return await new TenantIsolationProbe<Ledger>(tenant => new Ledger(options, tenant))
            .WithTenants("acme", "globex")
            .RunAsync(Token);
    }

    // The tenant is captured when the model is built; Tenant is what CurrentTenant reads.
    private sealed class Ledger(DbContextOptions<Ledger> options, string tenant) : DbContext(options)
    {
        public string Tenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var captured = Tenant;
            modelBuilder.Entity<Entry>(entry =>
            {
                entry.HasQueryFilter(row => row.TenantId == captured);
                entry.HasIndex(row => row.TenantId);
            });
        }
    }

    private sealed class TenantKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((Ledger)context).Tenant, designTime);
    }

    private sealed class SharedKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => new SharedKey(context.GetType(), designTime);
    }

    private sealed record SharedKey(Type Context, bool DesignTime);

    private sealed class Entry
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Memo { get; set; } = string.Empty;
    }
}
