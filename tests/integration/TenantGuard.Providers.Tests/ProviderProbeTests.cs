// <copyright file="ProviderProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Providers.Tests;

// The isolation probe on a real database server, with its transactions and ExecuteDelete: a
// guarded model passes, an unfiltered entity fails its reads and bulk delete, and a context without the
// write guard fails the guard checks. Each test gets its own database.
[FeatureDescription("The isolation probe on a real database server: a guarded model passes, an unfiltered entity fails its reads and bulk delete, and a context without the write guard fails the guard checks.")]
public abstract class ProviderProbeTests : FeatureFixture
{
    private static readonly string[] _guardChecks = ["UpdateGuard", "InsertGuard", "DeleteGuard"];

    private Func<Task<ProbeReport>> _probe = null!;
    private ProbeReport _report = null!;

    [Fact]
    public Task Guarded_model_passes_every_row()
        => Runner.RunScenarioAsync(
            Given_a_guarded_shop_on_the_server,
            When_the_probe_runs,
            Then_every_row_passes);

    [Fact]
    public Task Unfiltered_entity_fails_its_reads_and_bulk_delete_only()
        => Runner.RunScenarioAsync(
            Given_a_shop_on_the_server_that_leaves_customers_unfiltered,
            When_the_probe_runs,
            Then_only_the_customer_reads_and_bulk_delete_fail);

    [Fact]
    public Task Context_without_the_write_guard_fails_the_guard_rows_only()
        => Runner.RunScenarioAsync(
            Given_a_shop_on_the_server_without_the_write_guard,
            When_the_probe_runs,
            Then_only_the_guard_rows_fail);

    // Options for a new database with the given name on the server.
    protected abstract DbContextOptionsBuilder UseDatabase(DbContextOptionsBuilder builder, string database);

    // Note's key is natural on purpose, to seed a key EF doesn't generate, so its TGM010 is suppressed.
    private static Action<TenantGuardOptionsBuilder> Guard(Action<TenantGuardOptionsBuilder>? more = null)
        => tg =>
        {
            tg.CurrentTenant(context => ((ShopBase)context).CurrentTenant)
                .Suppress<Note>("TGM010", "the probe tests seed a key EF doesn't generate");
            more?.Invoke(tg);
        };

    private Task Given_a_guarded_shop_on_the_server()
        => UseServer((options, tenant) => new Shop(options, tenant), Guard());

    private Task Given_a_shop_on_the_server_that_leaves_customers_unfiltered()
        => UseServer((options, tenant) => new LeakyShop(options, tenant), Guard(tg => tg.Enforcement(Enforcement.Log)));

    private Task Given_a_shop_on_the_server_without_the_write_guard()
        => UseServer((options, tenant) => new Shop(options, tenant), tenantGuard: null);

    private async Task When_the_probe_runs() => _report = await _probe();

    private Task Then_every_row_passes()
    {
        _report.AssertAllPassed();
        return Task.CompletedTask;
    }

    private Task Then_only_the_customer_reads_and_bulk_delete_fail()
    {
        _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Select(row => (row.Entity, row.Check)).Should().Equal([("Customer", "ReadIsolation"), ("Customer", "FindIsolation"), ("Customer", "BulkIsolation")]);
        _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Should().AllSatisfy(row => row.Outcome.Should().Be(ProbeOutcome.Failed));
        return Task.CompletedTask;
    }

    private Task Then_only_the_guard_rows_fail()
    {
        var notPassed = _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        notPassed.Should().AllSatisfy(row => _guardChecks.Should().Contain(row.Check));
        notPassed.Count.Should().Be(12);
        return Task.CompletedTask;
    }

    // Creates a new database on the server; the probe runs against it in the When step.
    private async Task UseServer<TContext>(
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

        _probe = () => new TenantIsolationProbe<TContext>(tenant => create(options, tenant))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);
    }
}
