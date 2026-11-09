// <copyright file="HiddenRowProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;

namespace TenantGuard.Testing.Tests;

// The probe next to a soft-delete filter: the default seeder leaves the flag false, and a check whose
// row another filter hides from its own tenant is skipped rather than passed (docs/reference/probe.md).
public sealed class HiddenRowProbeTests
{
    private const string HiddenNote = "so the check proves nothing. Does another query filter, such as a soft delete, hide it?";

    private static readonly string[] _reads = ["ReadIsolation", "FindIsolation", "BulkIsolation"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_soft_delete_flag_is_seeded_false_so_every_row_passes()
    {
        await using var database = await ShopAsync(tenantFilter: true);

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
    }

    [Fact]
    public async Task A_soft_delete_only_filter_fails_the_reads_with_the_default_seeder()
    {
        await using var database = await ShopAsync(tenantFilter: false);

        var report = await database.Probe().RunAsync(Token);

        report.Rows.Where(row => row.Entity == "Order" && _reads.Contains(row.Check)).Select(row => (row.Check, row.Outcome))
            .Should().Equal(_reads.Select(check => (check, ProbeOutcome.Failed)));
    }

    [Fact]
    public async Task Rows_a_soft_delete_hides_from_their_own_tenant_skip_the_reads()
    {
        await using var database = await ShopAsync(tenantFilter: true);

        var report = await database.Probe().Seed<Order>((tenant, context) => new Order { IsDeleted = true }).RunAsync(Token);

        report.Rows.Where(row => row.Entity == "Order" && _reads.Contains(row.Check)).Select(row => (row.Check, row.Outcome, row.Detail)).Should().Equal(
            ("ReadIsolation", ProbeOutcome.Skipped, "As acme, Set<Order>() did not return acme's own row (Id: 1), so the check proves nothing. Does every context the factory creates use the same database?"),
            ("FindIsolation", ProbeOutcome.Skipped, $"As globex, FindAsync(Id: 2) did not return globex's own Order row, {HiddenNote}"),
            ("BulkIsolation", ProbeOutcome.Skipped, $"As globex, FindAsync(Id: 2) did not return globex's own Order row, {HiddenNote}"));
    }

    [Fact]
    public async Task A_navigation_to_or_from_a_hidden_row_is_skipped()
    {
        await using var database = await ShopAsync(tenantFilter: true);

        var report = await database.Probe().Seed<Order>((tenant, context) => new Order { IsDeleted = true }).RunAsync(Token);

        report.Rows.Where(row => row.Check.StartsWith("NavigationLeak", StringComparison.Ordinal)).Select(row => (row.Entity, row.Check, row.Outcome, row.Detail)).Should().Equal(
            ("Order", "NavigationLeak(Lines)", ProbeOutcome.Skipped, $"As acme, FindAsync(Id: 1) did not return acme's own Order row, {HiddenNote}"),
            ("OrderLine", "NavigationLeak(Order)", ProbeOutcome.Skipped, $"As globex, FindAsync(Id: 2) did not return globex's own Order row, {HiddenNote}"));
    }

    // An Order with a soft-delete flag and its OrderLines; tenantFilter false leaves the tenant out of
    // the Order's filter, and the context without UseTenantGuard, whose rules would refuse that model.
    private static Task<ProbeDatabase<Shop>> ShopAsync(bool tenantFilter)
        => ProbeDatabase<Shop>.CreateAsync(
            (options, tenant) => new Shop(options, tenant, tenantFilter),
            tenantFilter ? tg => tg.CurrentTenant(context => ((Shop)context).Tenant) : null);

    private sealed class Shop(DbContextOptions options, string? tenant, bool tenantFilter) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(order =>
            {
                order.HasMany(row => row.Lines).WithOne(line => line.Order).HasForeignKey(line => line.OrderId);
                if (tenantFilter)
                {
                    order.HasQueryFilter(row => row.TenantId == Tenant && !row.IsDeleted);
                }
                else
                {
                    order.HasQueryFilter(row => !row.IsDeleted);
                }
            });
            modelBuilder.Entity<OrderLine>().HasQueryFilter(line => line.TenantId == Tenant);
        }
    }

    private sealed class Order
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public List<OrderLine> Lines { get; set; } = [];
    }

    private sealed class OrderLine
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int OrderId { get; set; }

        public Order Order { get; set; } = null!;

        public string Product { get; set; } = string.Empty;
    }
}
