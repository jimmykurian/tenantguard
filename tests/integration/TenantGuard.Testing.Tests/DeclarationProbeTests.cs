// <copyright file="DeclarationProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing.Tests.Support;

namespace TenantGuard.Testing.Tests;

// The probe on cross-tenant data declared on purpose: an Unfiltered type isn't probed, a cross-tenant
// context is refused, and a tenant registry skips InsertGuard (docs/reference/probe.md).
public sealed class DeclarationProbeTests
{
    private static readonly string[] _checks = ["ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unfiltered_type_is_seeded_as_a_principal_but_not_probed()
    {
        await using var database = await ProbeDatabase<Club>.CreateAsync(
            (options, tenant) => new Club(options, tenant),
            tg => tg.CurrentTenant(context => ((Club)context).Tenant).Unfiltered<Membership>("maps users to their tenants"));

        var report = await database.Probe().RunAsync(Token);

        report.Rows.Select(row => (row.Entity, row.Check)).Should().Equal(_checks.Select(check => ("Seat", check)));
        report.AssertAllPassed();
    }

    [Fact]
    public async Task A_cross_tenant_context_has_nothing_to_probe()
    {
        await using var database = await ProbeDatabase<Club>.CreateAsync(
            (options, tenant) => new Club(options, tenant),
            tg => tg.CrossTenant("reports across tenants"));

        var exception = (await FluentActions.Awaiting(() => database.Probe().RunAsync(Token)).Should().ThrowExactlyAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Be("Club is declared cross-tenant, so there is no tenant isolation to probe.");
    }

    [Fact]
    public async Task A_tenant_registry_skips_InsertGuard_and_passes_the_other_checks()
    {
        await using var database = await ProbeDatabase<Registry>.CreateAsync(
            (options, tenant) => new Registry(options, tenant),
            tg => tg.CurrentTenant(context => ((Registry)context).Tenant).TenantProperty<Tenancy>(tenancy => tenancy.Id));

        var report = await database.Probe().RunAsync(Token);

        report.Rows.Select(row => (row.Check, row.Outcome)).Should().Equal(_checks.Select(check =>
            (check, check == "InsertGuard" ? ProbeOutcome.Skipped : ProbeOutcome.Passed)));
        report.Rows.Single(row => row.Check == "InsertGuard").Detail.Should().Be(
            "Tenancy's tenant property Id is its primary key, so a new row stamped globex would be globex's own row, which is already seeded.");
    }

    // A Seat, filtered by tenant, requires the Membership it belongs to, which has a tenant column and
    // no filter on purpose.
    private sealed class Club(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Membership>();
            modelBuilder.Entity<Seat>(seat =>
            {
                seat.HasOne(row => row.Membership).WithMany().HasForeignKey(row => row.MembershipId);
                seat.HasQueryFilter(row => row.TenantId == Tenant);
            });
        }
    }

    // The table of tenants, filtered on its own key.
    private sealed class Registry(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Tenancy>().HasQueryFilter(tenancy => tenancy.Id == Tenant);
    }

    private sealed class Membership
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;
    }

    private sealed class Seat
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public int MembershipId { get; set; }

        public Membership Membership { get; set; } = null!;

        public string Row { get; set; } = string.Empty;
    }

    private sealed class Tenancy
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
