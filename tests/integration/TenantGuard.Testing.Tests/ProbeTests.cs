// <copyright file="ProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Testing.Tests;

// The probe against itself: a guarded model passes, and each kind of leak fails the rows it should.
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
        report.Rows.Select(row => (row.Entity, row.Check)).Should().Equal(from entity in _shopEntities from check in _allChecks select (entity, check));
    }

    [Fact]
    public async Task Unfiltered_entity_fails_its_reads_and_bulk_delete_only()
    {
        await using var database = await ProbeDatabase<LeakyShop>.CreateAsync(
            (options, tenant) => new LeakyShop(options, tenant),
            Guard<ShopBase>(shop => shop.CurrentTenant, tg => tg.Enforcement(Enforcement.Log)));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Should().Equal([
                new ProbeRow("Customer", "ReadIsolation", ProbeOutcome.Failed, "As acme, Set<Customer>() returned globex's row (Id: 2). As globex, Set<Customer>() returned acme's row (Id: 1)."),
                new ProbeRow("Customer", "FindIsolation", ProbeOutcome.Failed, "As acme, FindAsync(Id: 2) returned globex's row. As globex, FindAsync(Id: 1) returned acme's row."),
                new ProbeRow("Customer", "BulkIsolation", ProbeOutcome.Failed, "As acme, ExecuteDelete removed globex's row (Id: 2). As globex, ExecuteDelete removed acme's row (Id: 1)."),
            ]);
        report.Rows.Count.Should().Be(24);
    }

    [Fact]
    public async Task Context_without_the_write_guard_fails_the_guard_rows_only()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), tenantGuard: null);

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var notPassed = report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        notPassed.Select(row => (row.Entity, row.Check, row.Outcome)).Should().Equal(from entity in _shopEntities from check in _guardChecks select (entity, check, ProbeOutcome.Failed));
        notPassed.Should().ContainSingle(row => row.Entity == "Customer" && row.Check == "UpdateGuard").Subject.Detail.Should().Be("As acme, SaveChanges updated globex's row (Id: 2); the context does not use UseTenantGuard. As globex, SaveChanges updated acme's row (Id: 1); the context does not use UseTenantGuard.");
    }

    [Fact]
    public async Task Cycle_of_required_relationships_is_skipped_as_needing_a_seeder()
    {
        await using var database = await ProbeDatabase<CycleShop>.CreateAsync((options, tenant) => new CycleShop(options, tenant), Guard<CycleShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        report.Rows.Where(row => row.Entity != "Plain").Should().AllSatisfy(row => (row.Outcome, row.Detail).Should().Be((ProbeOutcome.Skipped, "Needs a seeder: its required relationships form a cycle.")));
        report.Rows.Where(row => row.Entity == "Plain").Should().AllSatisfy(row => row.Outcome.Should().Be(ProbeOutcome.Passed));
        report.Rows.Count.Should().Be(18);
    }

    [Fact]
    public async Task Entity_the_default_seeder_cannot_create_is_skipped_with_its_dependents()
    {
        await using var database = await ProbeDatabase<SubscriptionShop>.CreateAsync(
            (options, tenant) => new SubscriptionShop(options, tenant),
            Guard<SubscriptionShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        report.Rows.Where(row => row.Outcome == ProbeOutcome.Skipped).Select(row => (row.Entity, row.Detail)).Distinct().Should().Equal([
                ("Seat", "Needs a seeder: its required principal Subscription cannot be seeded."),
                ("Subscription", "Needs a seeder: Subscription has no parameterless constructor."),
            ]);
        report.Rows.Should().NotContain(row => row.Outcome != ProbeOutcome.Skipped);
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
        (await acme.Set<Subscription>().SingleAsync(TestContext.Current.CancellationToken)).Plan.Should().Be("acme-gold");
    }

    [Fact]
    public async Task Shared_principal_is_seeded_once()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

        await database.Probe("acme", "globex", "initech").RunAsync(TestContext.Current.CancellationToken);

        await using var context = database.Create(tenant: null);
        (await context.Countries.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);
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

        var exception = (await FluentActions.Awaiting(() => database.Probe().RunAsync(TestContext.Current.CancellationToken)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("The tenant \"acme\" is not a Guid, the type of Ledger.TenantId. Pass WithTenants values the tenant property can hold.");
    }

    [Fact]
    public async Task Model_that_breaks_a_rule_throws_before_anything_is_seeded()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));
        var leaky = new DbContextOptionsBuilder()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(Guard<ShopBase>(shop => shop.CurrentTenant))
            .Options;

        await FluentActions.Awaiting(() => new TenantIsolationProbe<LeakyShop>(tenant => new LeakyShop(leaky, tenant)).WithTenants("acme", "globex").RunAsync(TestContext.Current.CancellationToken)).Should().ThrowExactlyAsync<TenantGuardModelException>();
    }

    [Fact]
    public async Task Model_without_tenant_scoped_entity_types_is_not_a_pass()
    {
        await using var database = await ProbeDatabase<LookupShop>.CreateAsync((options, tenant) => new LookupShop(options, tenant), Guard<LookupShop>(shop => shop.CurrentTenant));

        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var row = report.Rows.Should().ContainSingle().Subject;
        (row.Entity, row.Check, row.Outcome).Should().Be(("(model)", "Discovery", ProbeOutcome.Skipped));
        FluentActions.Invoking(report.AssertAllPassed).Should().ThrowExactly<TenantIsolationProbeException>();
    }

    [Fact]
    public async Task AssertAllPassed_throws_a_table_of_the_rows_that_did_not_pass()
    {
        await using var database = await ProbeDatabase<LeakyShop>.CreateAsync(
            (options, tenant) => new LeakyShop(options, tenant),
            Guard<ShopBase>(shop => shop.CurrentTenant, tg => tg.Enforcement(Enforcement.Log)));
        var report = await database.Probe().RunAsync(TestContext.Current.CancellationToken);

        var exception = FluentActions.Invoking(report.AssertAllPassed).Should().ThrowExactly<TenantIsolationProbeException>().Which;

        exception.Message.Should().Be("""
            TenantGuard isolation probe: 3 of 24 checks did not pass.

            Entity    Check          Outcome  Detail
            --------  -------------  -------  ------
            Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2). As globex, Set<Customer>() returned acme's row (Id: 1).
            Customer  FindIsolation  Failed   As acme, FindAsync(Id: 2) returned globex's row. As globex, FindAsync(Id: 1) returned acme's row.
            Customer  BulkIsolation  Failed   As acme, ExecuteDelete removed globex's row (Id: 2). As globex, ExecuteDelete removed acme's row (Id: 1).
            """.ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Seed_of_a_type_outside_the_model_throws()
    {
        await using var database = await ProbeDatabase<Shop>.CreateAsync((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

        var exception = (await FluentActions.Awaiting(() => database.Probe().Seed<Plain>((_, _) => new Plain()).RunAsync(TestContext.Current.CancellationToken)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Seed<Plain> names a type that is not an entity type of Shop.");
    }

    [Theory]
    [InlineData("acme", null)]
    [InlineData("acme", "acme")]
    [InlineData("acme", " ")]
    public void WithTenants_needs_two_distinct_tenants(string first, string? second)
    {
        var probe = new TenantIsolationProbe<Shop>(tenant => throw new InvalidOperationException("Not called."));
        string[] tenants = second is null ? [first] : [first, second];

        FluentActions.Invoking(() => probe.WithTenants(tenants)).Should().ThrowExactly<ArgumentException>();
    }

    [Fact]
    public async Task RunAsync_needs_tenants()
    {
        var probe = new TenantIsolationProbe<Shop>(tenant => throw new InvalidOperationException("Not called."));

        var exception = (await FluentActions.Awaiting(() => probe.RunAsync(TestContext.Current.CancellationToken)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Call WithTenants with at least two tenants before RunAsync.");
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
