// <copyright file="NavigationLeakTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;
using static TenantGuard.Testing.Tests.Models.NavigationModels;

namespace TenantGuard.Testing.Tests;

[FeatureDescription("NavigationLeak against itself: Include through a navigation loads no other tenant's rows, and each leak fails the navigation it goes through.")]
public sealed class NavigationLeakTests : FeatureFixture, IAsyncDisposable
{
    private readonly List<IAsyncDisposable> _databases = [];
    private Func<Task<ProbeReport>> _probe = null!;
    private Func<int> _rowCount = null!;
    private int _rowsAfter;
    private ProbeReport _report = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Every_navigation_to_a_tenant_scoped_type_gets_a_row()
        => Runner.RunScenarioAsync(
            Given_a_guarded_store,
            When_the_probe_runs,
            Then_every_row_passes,
            Then_each_tenant_scoped_navigation_has_a_NavigationLeak_row);

    [Fact]
    public Task Links_are_deleted_after_the_check()
        => Runner.RunScenarioAsync(
            Given_a_guarded_store,
            When_the_probe_runs,
            Then_the_database_holds_only_the_seeded_rows);

    [Fact]
    public Task Unfiltered_dependents_leak_through_the_collection()
        => Runner.RunScenarioAsync(
            Given_a_store_that_leaves_lines_unfiltered,
            When_the_probe_runs,
            Then_Basket_Lines_fails_naming_the_other_tenants_line,
            Then_Line_Basket_still_passes);

    [Fact]
    public Task Unfiltered_targets_leak_through_the_skip_navigation()
        => Runner.RunScenarioAsync(
            Given_a_store_that_leaves_tags_unfiltered,
            When_the_probe_runs,
            Then_Basket_Tags_fails_naming_the_other_tenants_tag,
            Then_Tag_Baskets_still_passes);

    [Fact]
    public Task One_to_one_link_whose_row_already_has_its_dependent_is_skipped_saying_why()
        => Runner.RunScenarioAsync(
            Given_a_directory_of_accounts_with_one_profile_each,
            When_the_probe_runs,
            Then_both_navigations_are_skipped_naming_the_unique_foreign_key);

    [Fact]
    public Task One_to_one_on_binary_keys_in_separate_arrays_is_skipped_saying_why()
        => Runner.RunScenarioAsync(
            Given_a_directory_of_accounts_keyed_by_bytes_with_one_profile_each,
            When_the_probe_runs,
            Then_both_binary_navigations_are_skipped_naming_the_unique_foreign_key);

    [Fact]
    public Task Optional_one_to_one_with_room_for_the_link_is_checked()
        => Runner.RunScenarioAsync(
            Given_a_directory_whose_profiles_leave_the_account_unset,
            When_the_probe_runs,
            Then_both_one_to_one_navigations_pass);

    public async ValueTask DisposeAsync()
    {
        foreach (var database in _databases)
        {
            await database.DisposeAsync();
        }
    }

    private static Action<TenantGuardOptionsBuilder> Guard<TContext>(Func<TContext, object?> currentTenant, bool log = false)
        where TContext : DbContext
        => tg =>
        {
            tg.CurrentTenant(context => currentTenant((TContext)context));
            if (log)
            {
                tg.Enforcement(Enforcement.Log);
            }
        };

    private static ProbeRow Row(ProbeReport report, string entity, string check)
        => report.Rows.Single(row => row.Entity == entity && row.Check == check);

    private async Task UseDatabase<TContext>(Func<DbContextOptions, string?, TContext> create, Action<TenantGuardOptionsBuilder> tenantGuard)
        where TContext : DbContext
    {
        var database = await ProbeDatabase<TContext>.CreateAsync(create, tenantGuard);
        _databases.Add(database);
        _probe = () => database.Probe().RunAsync(Token);
        _rowCount = () =>
        {
            // Counted in SQL, past the query filters; owned types share their owner's table.
            using var context = database.Create(tenant: null);
            var connection = context.Database.GetDbConnection();
            return context.Model.GetEntityTypes().Where(type => !type.IsOwned()).Sum(type =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM \"" + type.GetTableName() + "\"";
                return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            });
        };
    }

    private Task Given_a_guarded_store()
        => UseDatabase((options, tenant) => new Store(options, tenant), Guard<StoreBase>(store => store.CurrentTenant));

    // Under Enforcement.Log the model's TGM001 is logged, so the probe runs on the leaky model.
    private Task Given_a_store_that_leaves_lines_unfiltered()
        => UseDatabase((options, tenant) => new StoreWithUnfilteredLines(options, tenant), Guard<StoreBase>(store => store.CurrentTenant, log: true));

    private Task Given_a_store_that_leaves_tags_unfiltered()
        => UseDatabase((options, tenant) => new StoreWithUnfilteredTags(options, tenant), Guard<StoreBase>(store => store.CurrentTenant, log: true));

    private Task Given_a_directory_of_accounts_with_one_profile_each()
        => UseDatabase((options, tenant) => new Members(options, tenant), Guard<Members>(members => members.CurrentTenant));

    // The seeders give each profile's AccountId a new array equal to its account's key.
    private async Task Given_a_directory_of_accounts_keyed_by_bytes_with_one_profile_each()
    {
        await UseDatabase((options, tenant) => new BinaryMembers(options, tenant), Guard<BinaryMembers>(members => members.CurrentTenant, log: true));
        _probe = () => _databases.OfType<ProbeDatabase<BinaryMembers>>().Single().Probe()
            .Seed<BinaryAccount>((tenant, context) => new BinaryAccount { Id = [tenant == "acme" ? (byte)1 : (byte)2] })
            .Seed<BinaryProfile>((tenant, context) => new BinaryProfile { AccountId = [tenant == "acme" ? (byte)1 : (byte)2] })
            .RunAsync(Token);
    }

    private Task Given_a_directory_whose_profiles_leave_the_account_unset()
        => UseDatabase((options, tenant) => new OptionalMembers(options, tenant), Guard<OptionalMembers>(members => members.CurrentTenant));

    private async Task When_the_probe_runs()
    {
        _report = await _probe();
        _rowsAfter = _rowCount();
    }

    private Task Then_every_row_passes()
    {
        _report.Rows.Should().OnlyContain(row => row.Outcome == ProbeOutcome.Passed, string.Join("; ", _report.Rows.Where(row => row.Outcome != ProbeOutcome.Passed).Select(row => $"{row.Entity} {row.Check}: {row.Detail}")));
        return Task.CompletedTask;
    }

    private Task Then_each_tenant_scoped_navigation_has_a_NavigationLeak_row()
    {
        _report.Rows.Where(row => row.Check.StartsWith("NavigationLeak", StringComparison.Ordinal)).Select(row => $"{row.Entity} {row.Check}")
            .Should().Equal(
                "Basket NavigationLeak(Lines)",
                "Basket NavigationLeak(Parcels)",
                "Basket NavigationLeak(Tags)",
                "Line NavigationLeak(Basket)",
                "Parcel NavigationLeak(Basket)",
                "Tag NavigationLeak(Baskets)");

        // The navigation rows follow the entity's six checks.
        string[] basketChecks =
        [
            "ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard",
            "NavigationLeak(Lines)", "NavigationLeak(Parcels)", "NavigationLeak(Tags)",
        ];
        _report.Rows.Where(row => row.Entity == "Basket").Select(row => row.Check).Should().Equal(basketChecks);
        return Task.CompletedTask;
    }

    // Each tenant's seeded rows, a Region shared by both, and no link left over.
    private Task Then_the_database_holds_only_the_seeded_rows()
    {
        _rowsAfter.Should().Be(1 + (2 * 4));
        return Task.CompletedTask;
    }

    private Task Then_Basket_Lines_fails_naming_the_other_tenants_line()
    {
        var row = Row(_report, "Basket", "NavigationLeak(Lines)");
        row.Outcome.Should().Be(ProbeOutcome.Failed);
        row.Detail.Should().MatchRegex(@"^As acme, Include\(Lines\) on acme's Basket \(Id: \d+\) loaded globex's Line \(Id: \d+\)\. As globex, Include\(Lines\) on globex's Basket \(Id: \d+\) loaded acme's Line \(Id: \d+\)\.$");
        return Task.CompletedTask;
    }

    // Toward the principal, the filtered Basket hides the link, so nothing leaks.
    private Task Then_Line_Basket_still_passes()
    {
        Row(_report, "Line", "NavigationLeak(Basket)").Outcome.Should().Be(ProbeOutcome.Passed);
        return Task.CompletedTask;
    }

    private Task Then_Basket_Tags_fails_naming_the_other_tenants_tag()
    {
        var row = Row(_report, "Basket", "NavigationLeak(Tags)");
        row.Outcome.Should().Be(ProbeOutcome.Failed);
        row.Detail.Should().MatchRegex(@"^As acme, Include\(Tags\) on acme's Basket \(Id: \d+\) loaded globex's Tag \(Id: \d+\)\. As globex, Include\(Tags\) on globex's Basket \(Id: \d+\) loaded acme's Tag \(Id: \d+\)\.$");
        return Task.CompletedTask;
    }

    private Task Then_Tag_Baskets_still_passes()
    {
        Row(_report, "Tag", "NavigationLeak(Baskets)").Outcome.Should().Be(ProbeOutcome.Passed);
        return Task.CompletedTask;
    }

    private Task Then_both_navigations_are_skipped_naming_the_unique_foreign_key()
    {
        const string Occupied = @"^Profile's foreign key \(AccountId\) is unique, and (acme|globex)'s Account row \(Id: \d+\) already has a Profile, so a second one to link it across tenants would break the unique index\.$";
        foreach (var check in new[] { ("Account", "NavigationLeak(Profile)"), ("Profile", "NavigationLeak(Account)") })
        {
            var row = Row(_report, check.Item1, check.Item2);
            row.Outcome.Should().Be(ProbeOutcome.Skipped);
            row.Detail.Should().MatchRegex(Occupied);
        }

        return Task.CompletedTask;
    }

    private Task Then_both_binary_navigations_are_skipped_naming_the_unique_foreign_key()
    {
        const string Occupied = @"^BinaryProfile's foreign key \(AccountId\) is unique, and (acme|globex)'s BinaryAccount row \(Id: .+\) already has a BinaryProfile, so a second one to link it across tenants would break the unique index\.$";
        foreach (var check in new[] { ("BinaryAccount", "NavigationLeak(Profile)"), ("BinaryProfile", "NavigationLeak(Account)") })
        {
            var row = Row(_report, check.Item1, check.Item2);
            row.Outcome.Should().Be(ProbeOutcome.Skipped, row.Detail);
            row.Detail.Should().MatchRegex(Occupied);
        }

        return Task.CompletedTask;
    }

    private Task Then_both_one_to_one_navigations_pass()
    {
        Row(_report, "OptionalAccount", "NavigationLeak(Profile)").Outcome.Should().Be(ProbeOutcome.Passed);
        Row(_report, "OptionalProfile", "NavigationLeak(Account)").Outcome.Should().Be(ProbeOutcome.Passed);
        return Task.CompletedTask;
    }
}
