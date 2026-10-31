// <copyright file="ProviderProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Providers.Tests;

// The isolation probe on a real database server, with its transactions and ExecuteDelete: a
// guarded model passes, an unfiltered entity fails its reads and bulk delete, and a context without the
// write guard fails the guard checks. Each test gets its own database.
public abstract class ProviderProbeTests
{
    private static readonly string[] _guardChecks = ["UpdateGuard", "InsertGuard", "DeleteGuard"];

    [Fact]
    public async Task Guarded_model_passes_every_row()
    {
        var report = await ProbeAsync((options, tenant) => new Shop(options, tenant), Guard());

        report.AssertAllPassed();
    }

    [Fact]
    public async Task Unfiltered_entity_fails_its_reads_and_bulk_delete_only()
    {
        var report = await ProbeAsync((options, tenant) => new LeakyShop(options, tenant), Guard(tg => tg.Enforcement(Enforcement.Log)));

        Assert.Equal(
            [("Customer", "ReadIsolation"), ("Customer", "FindIsolation"), ("Customer", "BulkIsolation")],
            report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Select(row => (row.Entity, row.Check)));
        Assert.All(report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed), row => Assert.Equal(ProbeOutcome.Failed, row.Outcome));
    }

    [Fact]
    public async Task Context_without_the_write_guard_fails_the_guard_rows_only()
    {
        var report = await ProbeAsync((options, tenant) => new Shop(options, tenant), tenantGuard: null);

        var notPassed = report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        Assert.All(notPassed, row => Assert.Contains(row.Check, _guardChecks));
        Assert.Equal(12, notPassed.Count);
    }

    // Options for a new database with the given name on the server.
    protected abstract DbContextOptionsBuilder UseDatabase(DbContextOptionsBuilder builder, string database);

    private static Action<TenantGuardOptionsBuilder> Guard(Action<TenantGuardOptionsBuilder>? more = null)
        => tg =>
        {
            tg.CurrentTenant(context => ((ShopBase)context).CurrentTenant);
            more?.Invoke(tg);
        };

    private async Task<ProbeReport> ProbeAsync<TContext>(
        Func<DbContextOptions, string?, TContext> create,
        Action<TenantGuardOptionsBuilder>? tenantGuard)
        where TContext : DbContext
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var builder = UseDatabase(new DbContextOptionsBuilder(), $"probe_{Guid.NewGuid():N}");
        if (tenantGuard is not null)
        {
            builder.UseTenantGuard(tenantGuard);
        }

        var options = builder.Options;
        await using (var setup = create(options, null))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
        }

        return await new TenantIsolationProbe<TContext>(tenant => create(options, tenant))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);
    }
}
