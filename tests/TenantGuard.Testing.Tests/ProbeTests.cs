// <copyright file="ProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Testing.Tests;

// The probe against itself (SPEC 7): a guarded model passes, and each kind of leak fails the rows it should.
public sealed class ProbeTests
{
    private static readonly string[] _shopEntities = ["Customer", "Invoice", "Note", "Order"];

    private static readonly string[] _allChecks = ["ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard"];

    private static readonly string[] _guardChecks = ["UpdateGuard", "InsertGuard", "DeleteGuard"];

    [Fact]
    public async Task Guarded_model_passes_every_row()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        report.AssertAllPassed();
        Assert.Equal(
            from entity in _shopEntities from check in _allChecks select (entity, check),
            report.Rows.Select(row => (row.Entity, row.Check)));
    }

    [Fact]
    public async Task Unfiltered_entity_fails_its_reads_and_bulk_delete_only()
    {
        await using var database = await ProbeDatabase<LeakyShop>.CreateAsync(
            (options, tenant) => new LeakyShop(options, tenant),
            Guard<ShopBase>(shop => shop.CurrentTenant, tg => tg.Enforcement(Enforcement.Log)));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new ProbeRow("Customer", "ReadIsolation", ProbeOutcome.Failed, "As acme, Set<Customer>() returned globex's row (Id: 2). As globex, Set<Customer>() returned acme's row (Id: 1)."),
                new ProbeRow("Customer", "FindIsolation", ProbeOutcome.Failed, "As acme, FindAsync(Id: 2) returned globex's row. As globex, FindAsync(Id: 1) returned acme's row."),
                new ProbeRow("Customer", "BulkIsolation", ProbeOutcome.Failed, "As acme, ExecuteDelete removed globex's row (Id: 2). As globex, ExecuteDelete removed acme's row (Id: 1)."),
            ],
            report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed));
        Assert.Equal(24, report.Rows.Count);
    }

    [Fact]
    public async Task Context_without_the_write_guard_fails_the_guard_rows_only()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), tenantGuard: null);

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var notPassed = report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        Assert.Equal(
            from entity in _shopEntities from check in _guardChecks select (entity, check, ProbeOutcome.Failed),
            notPassed.Select(row => (row.Entity, row.Check, row.Outcome)));
        Assert.Equal(
            "As acme, SaveChanges updated globex's row (Id: 2); the context does not use UseTenantGuard. As globex, SaveChanges updated acme's row (Id: 1); the context does not use UseTenantGuard.",
            Assert.Single(notPassed, row => row is { Entity: "Customer", Check: "UpdateGuard" }).Detail);
    }

    [Fact]
    public async Task Cycle_of_required_relationships_is_skipped_as_needing_a_seeder()
    {
        await using var database = await ProbeDatabase<CycleShop>.CreateAsync((options, tenant) => new CycleShop(options, tenant), Guard<CycleShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        Assert.All(
            report.Rows.Where(row => row.Entity != "Plain"),
            row => Assert.Equal((ProbeOutcome.Skipped, "Needs a seeder: its required relationships form a cycle."), (row.Outcome, row.Detail)));
        Assert.All(report.Rows.Where(row => row.Entity == "Plain"), row => Assert.Equal(ProbeOutcome.Passed, row.Outcome));
        Assert.Equal(18, report.Rows.Count);
    }

    [Fact]
    public async Task Entity_the_default_seeder_cannot_create_is_skipped_with_its_dependents()
    {
        await using var database = await ProbeDatabase<SubscriptionShop>.CreateAsync(
            (options, tenant) => new SubscriptionShop(options, tenant),
            Guard<SubscriptionShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("Seat", "Needs a seeder: its required principal Subscription cannot be seeded."),
                ("Subscription", "Needs a seeder: Subscription has no parameterless constructor."),
            ],
            report.Rows.Where(row => row.Outcome == ProbeOutcome.Skipped).Select(row => (row.Entity, row.Detail)).Distinct());
        Assert.DoesNotContain(report.Rows, row => row.Outcome != ProbeOutcome.Skipped);
    }

    [Fact]
    public async Task Seed_replaces_the_default_seeder()
    {
        await using var database = await ProbeDatabase<SubscriptionShop>.CreateAsync(
            (options, tenant) => new SubscriptionShop(options, tenant),
            Guard<SubscriptionShop>(shop => shop.CurrentTenant));

        var report = await database.Probe()
            .Seed<Subscription>((tenant, _) => new Subscription($"{tenant}-gold"))
            .RunAsync(TestContext.Current.CancellationToken);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        Assert.Equal("acme-gold", (await acme.Set<Subscription>().SingleAsync(TestContext.Current.CancellationToken)).Plan);
    }

    [Fact]
    public async Task Shared_principal_is_seeded_once()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

        await database.Probe("acme", "globex", "initech").RunAsync(TestContext.Current.CancellationToken);

        await using var context = database.Create(tenant: null);
        Assert.Equal(1, await context.Countries.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Guid_tenants_are_stamped_as_Guids()
    {
        await using var database = await ProbeDatabase<GuidShop>.CreateAsync((options, tenant) => new GuidShop(options, tenant), Guard<GuidShop>(shop => shop.CurrentTenant));

        var report = await database.Probe("6f9619ff-8b86-d011-b42d-00c04fc964ff", "0c3a7a3e-58b4-4f3c-9b7d-1d2a3e4f5a6b").RunAsync(TestContext.Current.CancellationToken);

        report.AssertAllPassed();
    }

    [Fact]
    public async Task Tenant_the_tenant_property_cannot_hold_throws()
    {
        await using var database = await ProbeDatabase<GuidShop>.CreateAsync((options, tenant) => new GuidShop(options, tenant), Guard<GuidShop>(shop => shop.CurrentTenant));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => database.Probe().RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "The tenant \"acme\" is not a Guid, the type of Ledger.TenantId. Pass WithTenants values the tenant property can hold.",
            exception.Message);
    }

    [Fact]
    public async Task Model_that_breaks_a_rule_throws_before_anything_is_seeded()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));
        var leaky = new DbContextOptionsBuilder()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(Guard<ShopBase>(shop => shop.CurrentTenant))
            .Options;

        await Assert.ThrowsAsync<TenantGuardModelException>(
            () => new TenantIsolationProbe<LeakyShop>(tenant => new LeakyShop(leaky, tenant)).WithTenants("acme", "globex").RunAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Model_without_tenant_scoped_entity_types_is_not_a_pass()
    {
        await using var database = await ProbeDatabase<LookupShop>.CreateAsync((options, tenant) => new LookupShop(options, tenant), Guard<LookupShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var row = Assert.Single(report.Rows);
        Assert.Equal(("(model)", "Discovery", ProbeOutcome.Skipped), (row.Entity, row.Check, row.Outcome));
        Assert.Throws<TenantIsolationProbeException>(report.AssertAllPassed);
    }

    [Fact]
    public async Task AssertAllPassed_throws_a_table_of_the_rows_that_did_not_pass()
    {
        await using var database = await ProbeDatabase<LeakyShop>.CreateAsync(
            (options, tenant) => new LeakyShop(options, tenant),
            Guard<ShopBase>(shop => shop.CurrentTenant, tg => tg.Enforcement(Enforcement.Log)));
        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var exception = Assert.Throws<TenantIsolationProbeException>(report.AssertAllPassed);

        Assert.Equal(
            """
            TenantGuard isolation probe: 3 of 24 checks did not pass.

            Entity    Check          Outcome  Detail
            --------  -------------  -------  ------
            Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). As globex, Set<Customer>() returned acme's row (Id: 1).
            Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. As globex, FindAsync(Id: 1) returned acme's row.
            Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). As globex, ExecuteDelete removed acme's row (Id: 1).
            """.ReplaceLineEndings("\n"),
            exception.Message);
    }

    [Fact]
    public async Task Seed_of_a_type_outside_the_model_throws()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => database.Probe().Seed<Plain>((_, _) => new Plain()).RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Seed<Plain> names a type that is not an entity type of Shop.", exception.Message);
    }

    [Theory]
    [InlineData("acme", null)]
    [InlineData("acme", "acme")]
    [InlineData("acme", " ")]
    public void WithTenants_needs_two_distinct_tenants(string first, string? second)
    {
        var probe = new TenantIsolationProbe<Shop>(tenant => throw new InvalidOperationException("Not called."));
        string[] tenants = second is null ? [first] : [first, second];

        Assert.Throws<ArgumentException>(() => probe.WithTenants(tenants));
    }

    [Fact]
    public async Task RunAsync_needs_tenants()
    {
        var probe = new TenantIsolationProbe<Shop>(tenant => throw new InvalidOperationException("Not called."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => probe.RunAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Call WithTenants with at least two tenants before RunAsync.", exception.Message);
    }

    // UseTenantGuard with CurrentTenant read from the context, plus anything else the test needs.
    private static Action<TenantGuardOptionsBuilder> Guard<TContext>(Func<TContext, object?> currentTenant, Action<TenantGuardOptionsBuilder>? more = null)
        where TContext : DbContext
        => tg =>
        {
            tg.CurrentTenant(context => currentTenant((TContext)context));
            more?.Invoke(tg);
        };
}
