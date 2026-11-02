// <copyright file="ProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Bogus;
using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Testing.Tests;

[FeatureDescription("The probe against itself: a guarded model passes, and each kind of leak fails the rows it should.")]
public sealed class ProbeTests : FeatureFixture, IAsyncDisposable
{
    private static readonly string[] _shopEntities = ["Customer", "Invoice", "Note", "Order"];

    private static readonly string[] _allChecks = ["ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard"];

    private static readonly string[] _guardChecks = ["UpdateGuard", "InsertGuard", "DeleteGuard"];

    private readonly List<IAsyncDisposable> _databases = [];
    private readonly List<string> _seededValues = [];
    private Func<string[], Task<ProbeReport>> _probe = null!;
    private Func<object, object> _configure = probe => probe;
    private Func<string?, DbContext> _create = null!;
    private ProbeReport _report = null!;
    private Exception? _failure;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Guarded_model_passes_every_row()
        => Runner.RunScenarioAsync(
            Given_a_guarded_shop,
            When_the_probe_runs,
            Then_every_row_passes,
            Then_there_is_a_row_for_every_entity_and_check);

    [Fact]
    public Task Unfiltered_entity_fails_its_reads_and_bulk_delete_only()
        => Runner.RunScenarioAsync(
            Given_a_shop_that_leaves_customers_unfiltered,
            When_the_probe_runs,
            Then_only_the_customer_reads_and_bulk_delete_fail);

    [Fact]
    public Task Context_without_the_write_guard_fails_the_guard_rows_only()
        => Runner.RunScenarioAsync(
            Given_a_shop_without_the_write_guard,
            When_the_probe_runs,
            Then_only_the_guard_rows_fail);

    [Fact]
    public Task Cycle_of_required_relationships_is_skipped_as_needing_a_seeder()
        => Runner.RunScenarioAsync(
            Given_a_model_whose_required_relationships_form_a_cycle,
            When_the_probe_runs,
            Then_the_cycle_is_skipped_as_needing_a_seeder);

    [Fact]
    public Task Entity_the_default_seeder_cannot_create_is_skipped_with_its_dependents()
        => Runner.RunScenarioAsync(
            Given_a_model_with_an_entity_the_default_seeder_cannot_create,
            When_the_probe_runs,
            Then_that_entity_and_its_dependents_are_skipped);

    [Fact]
    public Task Seed_replaces_the_default_seeder()
        => Runner.RunScenarioAsync(
            Given_a_model_with_an_entity_the_default_seeder_cannot_create,
            Given_a_seeder_that_gives_each_tenant_a_gold_subscription,
            When_the_probe_runs,
            Then_every_row_passes,
            Then_acmes_subscription_came_from_the_seeder);

    [Fact]
    public Task Shared_principal_is_seeded_once()
        => Runner.RunScenarioAsync(
            Given_a_guarded_shop,
            When_the_probe_runs_for_acme_globex_and_initech,
            Then_the_shared_country_was_seeded_once);

    [Fact]
    public Task Guid_tenants_are_stamped_as_Guids()
        => Runner.RunScenarioAsync(
            Given_a_model_whose_tenant_is_a_Guid,
            When_the_probe_runs_for_two_Guid_tenants,
            Then_every_row_passes);

    [Fact]
    public Task Tenant_the_tenant_property_cannot_hold_throws()
        => Runner.RunScenarioAsync(
            Given_a_model_whose_tenant_is_a_Guid,
            When_the_probe_is_started_for_acme_and_globex,
            Then_it_fails_naming_the_tenant_and_the_property);

    [Fact]
    public Task Model_that_breaks_a_rule_throws_before_anything_is_seeded()
        => Runner.RunScenarioAsync(
            Given_a_model_that_breaks_a_rule,
            When_the_probe_is_started_for_acme_and_globex,
            Then_it_fails_with_the_model_report);

    [Fact]
    public Task Model_without_tenant_scoped_entity_types_is_not_a_pass()
        => Runner.RunScenarioAsync(
            Given_a_model_without_tenant_scoped_entity_types,
            When_the_probe_runs,
            Then_the_only_row_is_a_skipped_discovery,
            Then_AssertAllPassed_throws);

    [Fact]
    public Task AssertAllPassed_throws_a_table_of_the_rows_that_did_not_pass()
        => Runner.RunScenarioAsync(
            Given_a_shop_that_leaves_customers_unfiltered,
            When_the_probe_runs,
            Then_AssertAllPassed_throws_the_snapshotted_table);

    [Fact]
    public Task The_report_names_no_seeded_entity_values()
        => Runner.RunScenarioAsync(
            Given_a_shop_that_leaves_customers_unfiltered,
            Given_seeders_that_fill_customers_and_invoices_with_Bogus_values,
            When_the_probe_runs,
            Then_a_leak_is_reported,
            Then_neither_the_rows_nor_the_table_name_a_seeded_value);

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

    public async ValueTask DisposeAsync()
    {
        foreach (var database in _databases)
        {
            await database.DisposeAsync();
        }
    }

    // UseTenantGuard with CurrentTenant read from the context, plus anything else the test needs.
    private static Action<TenantGuardOptionsBuilder> Guard<TContext>(Func<TContext, object?> currentTenant, Action<TenantGuardOptionsBuilder>? more = null)
        where TContext : DbContext
        => tg =>
        {
            tg.CurrentTenant(context => currentTenant((TContext)context));
            more?.Invoke(tg);
        };

    // The probe runs against this database when a When step runs it, with what later Given steps
    // added through _configure.
    private async Task UseDatabase<TContext>(Func<DbContextOptions, string?, TContext> create, Action<TenantGuardOptionsBuilder>? tenantGuard)
        where TContext : DbContext
    {
        var database = await ProbeDatabase<TContext>.CreateAsync(create, tenantGuard);
        _databases.Add(database);
        _create = tenant => database.Create(tenant);
        _probe = tenants => ((TenantIsolationProbe<TContext>)_configure(database.Probe(tenants))).RunAsync(Token);
    }

    private Task Given_a_guarded_shop()
        => UseDatabase((options, tenant) => new Shop(options, tenant), Guard<ShopBase>(shop => shop.CurrentTenant));

    private Task Given_a_shop_that_leaves_customers_unfiltered()
        => UseDatabase(
            (options, tenant) => new LeakyShop(options, tenant),
            Guard<ShopBase>(shop => shop.CurrentTenant, tg => tg.Enforcement(Enforcement.Log)));

    private Task Given_a_shop_without_the_write_guard()
        => UseDatabase((options, tenant) => new Shop(options, tenant), tenantGuard: null);

    private Task Given_a_model_whose_required_relationships_form_a_cycle()
        => UseDatabase((options, tenant) => new CycleShop(options, tenant), Guard<CycleShop>(shop => shop.CurrentTenant));

    private Task Given_a_model_with_an_entity_the_default_seeder_cannot_create()
        => UseDatabase((options, tenant) => new SubscriptionShop(options, tenant), Guard<SubscriptionShop>(shop => shop.CurrentTenant));

    private Task Given_a_seeder_that_gives_each_tenant_a_gold_subscription()
    {
        _configure = probe => ((TenantIsolationProbe<SubscriptionShop>)probe).Seed<Subscription>((tenant, _) => new Subscription($"{tenant}-gold"));
        return Task.CompletedTask;
    }

    private Task Given_a_model_whose_tenant_is_a_Guid()
        => UseDatabase((options, tenant) => new GuidShop(options, tenant), Guard<GuidShop>(shop => shop.CurrentTenant));

    private Task Given_a_model_without_tenant_scoped_entity_types()
        => UseDatabase((options, tenant) => new LookupShop(options, tenant), Guard<LookupShop>(shop => shop.CurrentTenant));

    private Task Given_a_model_that_breaks_a_rule()
    {
        var leaky = new DbContextOptionsBuilder()
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(Guard<ShopBase>(shop => shop.CurrentTenant))
            .Options;
        _probe = tenants => new TenantIsolationProbe<LeakyShop>(tenant => new LeakyShop(leaky, tenant)).WithTenants(tenants).RunAsync(Token);
        return Task.CompletedTask;
    }

    private Task Given_seeders_that_fill_customers_and_invoices_with_Bogus_values()
    {
        // Bogus fills every value but the keys and tenants, with text that can't turn up by chance.
        var faker = new Faker { Random = new Randomizer(7) };
        string Fake(Func<Faker, string> realistic)
        {
            lock (_seededValues)
            {
                var value = $"{realistic(faker)} {faker.Random.AlphaNumeric(12)}";
                _seededValues.Add(value);
                return value;
            }
        }

        _configure = probe => ((TenantIsolationProbe<LeakyShop>)probe)
            .Seed<Customer>((_, _) => new Customer { Name = Fake(f => f.Name.FullName()) })
            .Seed<Invoice>((_, _) => new Invoice { Number = Fake(f => f.Commerce.ProductName()), Memo = Fake(f => f.Lorem.Sentence()) });
        return Task.CompletedTask;
    }

    private async Task When_the_probe_runs() => _report = await _probe([]);

    private async Task When_the_probe_runs_for_acme_globex_and_initech() => _report = await _probe(["acme", "globex", "initech"]);

    private async Task When_the_probe_runs_for_two_Guid_tenants()
        => _report = await _probe(["6f9619ff-8b86-d011-b42d-00c04fc964ff", "0c3a7a3e-58b4-4f3c-9b7d-1d2a3e4f5a6b"]);

    private async Task When_the_probe_is_started_for_acme_and_globex()
        => _failure = await Record.ExceptionAsync(() => _probe(["acme", "globex"]));

    private Task Then_every_row_passes()
    {
        _report.AssertAllPassed();
        return Task.CompletedTask;
    }

    private Task Then_there_is_a_row_for_every_entity_and_check()
    {
        _report.Rows.Select(row => (row.Entity, row.Check)).Should().Equal(from entity in _shopEntities from check in _allChecks select (entity, check));
        return Task.CompletedTask;
    }

    private Task Then_only_the_customer_reads_and_bulk_delete_fail()
    {
        _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Should().Equal([
                new ProbeRow("Customer", "ReadIsolation", ProbeOutcome.Failed, "As acme, Set<Customer>() returned globex's row (Id: 2). As globex, Set<Customer>() returned acme's row (Id: 1)."),
                new ProbeRow("Customer", "FindIsolation", ProbeOutcome.Failed, "As acme, FindAsync(Id: 2) returned globex's row. As globex, FindAsync(Id: 1) returned acme's row."),
                new ProbeRow("Customer", "BulkIsolation", ProbeOutcome.Failed, "As acme, ExecuteDelete removed globex's row (Id: 2). As globex, ExecuteDelete removed acme's row (Id: 1)."),
            ]);
        _report.Rows.Count.Should().Be(24);
        return Task.CompletedTask;
    }

    private Task Then_only_the_guard_rows_fail()
    {
        var notPassed = _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        notPassed.Select(row => (row.Entity, row.Check, row.Outcome)).Should().Equal(from entity in _shopEntities from check in _guardChecks select (entity, check, ProbeOutcome.Failed));
        notPassed.Should().ContainSingle(row => row.Entity == "Customer" && row.Check == "UpdateGuard").Subject.Detail.Should().Be("As acme, SaveChanges updated globex's row (Id: 2); the context does not use UseTenantGuard. As globex, SaveChanges updated acme's row (Id: 1); the context does not use UseTenantGuard.");
        return Task.CompletedTask;
    }

    private Task Then_the_cycle_is_skipped_as_needing_a_seeder()
    {
        _report.Rows.Where(row => row.Entity != "Plain").Should().AllSatisfy(row => (row.Outcome, row.Detail).Should().Be((ProbeOutcome.Skipped, "Needs a seeder: its required relationships form a cycle.")));
        _report.Rows.Where(row => row.Entity == "Plain").Should().AllSatisfy(row => row.Outcome.Should().Be(ProbeOutcome.Passed));
        _report.Rows.Count.Should().Be(18);
        return Task.CompletedTask;
    }

    private Task Then_that_entity_and_its_dependents_are_skipped()
    {
        _report.Rows.Where(row => row.Outcome == ProbeOutcome.Skipped).Select(row => (row.Entity, row.Detail)).Distinct().Should().Equal([
                ("Seat", "Needs a seeder: its required principal Subscription cannot be seeded."),
                ("Subscription", "Needs a seeder: Subscription has no parameterless constructor."),
            ]);
        _report.Rows.Should().NotContain(row => row.Outcome != ProbeOutcome.Skipped);
        return Task.CompletedTask;
    }

    private async Task Then_acmes_subscription_came_from_the_seeder()
    {
        await using var acme = _create("acme");
        (await acme.Set<Subscription>().SingleAsync(Token)).Plan.Should().Be("acme-gold");
    }

    private async Task Then_the_shared_country_was_seeded_once()
    {
        await using var context = _create(null);
        (await context.Set<Country>().CountAsync(Token)).Should().Be(1);
    }

    private Task Then_it_fails_naming_the_tenant_and_the_property()
    {
        _failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("The tenant \"acme\" is not a Guid, the type of Ledger.TenantId. Pass WithTenants values the tenant property can hold.");
        return Task.CompletedTask;
    }

    private Task Then_it_fails_with_the_model_report()
    {
        _failure.Should().BeOfType<TenantGuardModelException>();
        return Task.CompletedTask;
    }

    private Task Then_the_only_row_is_a_skipped_discovery()
    {
        var row = _report.Rows.Should().ContainSingle().Subject;
        (row.Entity, row.Check, row.Outcome).Should().Be(("(model)", "Discovery", ProbeOutcome.Skipped));
        return Task.CompletedTask;
    }

    private Task Then_AssertAllPassed_throws()
    {
        FluentActions.Invoking(_report.AssertAllPassed).Should().ThrowExactly<TenantIsolationProbeException>();
        return Task.CompletedTask;
    }

    // The table is the probe's output to the user; the snapshot is next to this file.
    private async Task Then_AssertAllPassed_throws_the_snapshotted_table()
    {
        var exception = FluentActions.Invoking(_report.AssertAllPassed).Should().ThrowExactly<TenantIsolationProbeException>().Which;
        await Verify(exception.Message);
    }

    private Task Then_a_leak_is_reported()
    {
        _report.Rows.Should().Contain(row => row.Outcome == ProbeOutcome.Failed);
        _seededValues.Should().NotBeEmpty();
        return Task.CompletedTask;
    }

    private Task Then_neither_the_rows_nor_the_table_name_a_seeded_value()
    {
        var table = FluentActions.Invoking(_report.AssertAllPassed).Should().ThrowExactly<TenantIsolationProbeException>().Which.Message;
        string.Join("\n", _report.Rows.Select(row => row.Detail).Append(table)).Should().NotContainAny(_seededValues);
        return Task.CompletedTask;
    }
}
