// <copyright file="NoTenantProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TenantGuard.Testing.Tests.Support;

namespace TenantGuard.Testing.Tests;

// NoTenantIsolation, with WithoutTenant: a context with no tenant reads none of the tenants' rows
// (docs/reference/probe.md, "A context with no tenant").
public sealed class NoTenantProbeTests
{
    private static readonly string[] _checks = ["ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_filter_on_the_tenant_alone_passes_in_a_last_row()
    {
        await using var database = await BookAsync<TenantBook>();

        var report = await database.Probe().WithoutTenant(() => database.Create(tenant: null)).RunAsync(Token);

        report.Rows.Where(row => row.Entity == "Entry").Select(row => (row.Check, row.Outcome, row.Detail)).Should().Equal(
            [.. _checks.Select(check => (check, ProbeOutcome.Passed, string.Empty)), ("NoTenantIsolation", ProbeOutcome.Passed, string.Empty)]);
    }

    [Fact]
    public async Task Without_WithoutTenant_the_report_has_no_NoTenantIsolation_row()
    {
        await using var database = await BookAsync<OpenBook>();

        var report = await database.Probe().RunAsync(Token);

        report.Rows.Where(row => row.Entity == "Entry").Select(row => row.Check).Should().Equal(_checks);
    }

    [Fact]
    public async Task A_filter_open_without_a_tenant_fails_naming_every_row_it_read()
    {
        await using var database = await BookAsync<OpenBook>();

        var report = await database.Probe("acme", "globex", "initech").WithoutTenant(() => database.Create(tenant: null)).RunAsync(Token);

        const string Detail = "As no tenant, Set<Entry>() returned acme's row (Id: 1), globex's row (Id: 2) and initech's row (Id: 3). "
            + "As no tenant, FindAsync(Id: 1) returned acme's row. As no tenant, FindAsync(Id: 2) returned globex's row. As no tenant, FindAsync(Id: 3) returned initech's row.";
        report.Rows.Should().ContainSingle(row => row.Entity == "Entry" && row.Check == "NoTenantIsolation").Which.Should().Be(new ProbeRow("Entry", "NoTenantIsolation", ProbeOutcome.Failed, Detail));
        report.Rows.Where(row => row.Entity == "Entry" && row.Check != "NoTenantIsolation").Should().OnlyContain(row => row.Outcome == ProbeOutcome.Passed);
    }

    [Fact]
    public async Task A_query_that_throws_without_a_tenant_passes_naming_the_exception()
    {
        await using var database = await BookAsync<TenantBook>(throwWithoutTenant: true);

        var report = await database.Probe().WithoutTenant(() => database.Create(tenant: null)).RunAsync(Token);

        var row = report.Rows.Single(row => row.Entity == "Entry" && row.Check == "NoTenantIsolation");
        row.Outcome.Should().Be(ProbeOutcome.Passed);
        row.Detail.Should().Be(
            "As no tenant, Set<Entry>() threw InvalidOperationException, so it returned no rows: There is no current tenant. "
            + "As no tenant, FindAsync threw InvalidOperationException, so it returned no row: There is no current tenant.");
    }

    [Fact]
    public async Task An_exception_from_the_factory_is_thrown_from_RunAsync()
    {
        await using var database = await BookAsync<TenantBook>();

        var probe = database.Probe().WithoutTenant(() => throw new NotSupportedException("No context without a tenant."));

        await FluentActions.Awaiting(() => probe.RunAsync(Token)).Should().ThrowExactlyAsync<NotSupportedException>().WithMessage("No context without a tenant.");
    }

    [Fact]
    public async Task Rows_hidden_from_their_own_tenant_skip_the_check()
    {
        await using var database = await BookAsync<TenantBook>();

        var report = await database.Probe()
            .Seed<Entry>((tenant, context) => new Entry { IsDeleted = true })
            .WithoutTenant(() => database.Create(tenant: null))
            .RunAsync(Token);

        report.Rows.Single(row => row.Entity == "Entry" && row.Check == "NoTenantIsolation").Should().Be(new ProbeRow(
            "Entry", "NoTenantIsolation", ProbeOutcome.Skipped, "No tenant could read or find its own Entry row, so the check proves nothing."));
    }

    [Fact]
    public async Task A_type_the_probe_cannot_probe_is_skipped_with_the_same_detail()
    {
        await using var database = await BookAsync<TenantBook>();

        var report = await database.Probe().WithoutTenant(() => database.Create(tenant: null)).RunAsync(Token);

        report.Rows.Where(row => row.Entity == "EntryTotal").Select(row => (row.Check, row.Outcome, row.Detail)).Should().Equal(
            [.. _checks.Append("NoTenantIsolation").Select(check => (check, ProbeOutcome.Skipped, "It is keyless, so the probe cannot write or find its rows."))]);
    }

    [Fact]
    public async Task The_host_under_NullTenantIsHost_is_checked_the_same()
    {
        await using var database = await BookAsync<TenantBook>(host: true);

        var report = await database.Probe().WithoutTenant(() => database.Create(tenant: null)).RunAsync(Token);

        report.Rows.Single(row => row.Entity == "Entry" && row.Check == "NoTenantIsolation").Outcome.Should().Be(ProbeOutcome.Passed);
    }

    [Fact]
    public async Task A_cross_tenant_model_without_a_tenant_skips_the_check()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(Token);
        var options = new DbContextOptionsBuilder<Ledger>()
            .UseSqlite(connection)
            .ReplaceService<IModelCacheKeyFactory, LedgerKeyFactory>()
            .UseTenantGuard(tg => tg
                .AllowCapturedValues()
                .CrossTenantWhenNoTenant("background jobs work across tenants")
                .CurrentTenant(context => ((Ledger)context).Tenant))
            .Options;
        await using (var setup = new Ledger(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(Token);
        }

        var report = await new TenantIsolationProbe<Ledger>(tenant => new Ledger(options, tenant))
            .WithTenants("acme", "globex")
            .WithoutTenant(() => new Ledger(options, tenant: null))
            .RunAsync(Token);

        report.Rows.Single(row => row.Check == "NoTenantIsolation").Should().Be(new ProbeRow(
            "Posting", "NoTenantIsolation", ProbeOutcome.Skipped, "The context with no tenant is declared cross-tenant by CrossTenantWhenNoTenant."));
    }

    [Fact]
    public void A_null_factory_throws()
        => FluentActions.Invoking(() => new TenantIsolationProbe<TenantBook>(tenant => null!).WithoutTenant(null!)).Should().ThrowExactly<ArgumentNullException>();

    private static Task<ProbeDatabase<TBook>> BookAsync<TBook>(bool throwWithoutTenant = false, bool host = false)
        where TBook : Book
        => ProbeDatabase<TBook>.CreateAsync(
            (options, tenant) => (TBook)Activator.CreateInstance(typeof(TBook), options, tenant, throwWithoutTenant)!,
            tg =>
            {
                tg.CurrentTenant(context => ((Book)context).Tenant);
                if (host)
                {
                    tg.NullTenantIsHost();
                }
            });

    // TenantId == tenant: no rows with no tenant.
    private sealed class TenantBook(DbContextOptions options, string? tenant, bool throwWithoutTenant) : Book(options, tenant, throwWithoutTenant)
    {
        protected override Expression<Func<Entry, bool>> Filter => row => row.TenantId == FilterTenant && !row.IsDeleted;
    }

    // tenant == null || TenantId == tenant: every row with no tenant (TGM013).
    private sealed class OpenBook(DbContextOptions options, string? tenant, bool throwWithoutTenant) : Book(options, tenant, throwWithoutTenant)
    {
        protected override Expression<Func<Entry, bool>> Filter => row => FilterTenant == null || row.TenantId == FilterTenant;
    }

    // Entry is filtered as each context says; EntryTotal is a keyless view of it, which the probe can't
    // probe. One context type per filter, since EF Core caches the model by context type.
    // throwWithoutTenant makes the filter's tenant throw when there is none, as some apps do.
    private abstract class Book(DbContextOptions options, string? tenant, bool throwWithoutTenant) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;

        public string? FilterTenant => Tenant is null && throwWithoutTenant ? throw new InvalidOperationException("There is no current tenant.") : Tenant;

        protected abstract Expression<Func<Entry, bool>> Filter { get; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Entry>(entry =>
            {
                entry.HasIndex(row => row.TenantId);
                entry.HasQueryFilter(Filter);
            });
            modelBuilder.Entity<EntryTotal>(total =>
            {
                total.HasNoKey().ToView("entry_totals");
                total.HasQueryFilter(row => row.TenantId == FilterTenant);
            });
        }
    }

    private sealed class Entry
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public string Memo { get; set; } = string.Empty;
    }

    private sealed class EntryTotal
    {
        public string TenantId { get; set; } = string.Empty;

        public int Count { get; set; }
    }

    // The tenant is captured when the model is built: with no tenant, the model has no filter.
    private sealed class Ledger(DbContextOptions<Ledger> options, string? tenant) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var captured = Tenant;
            modelBuilder.Entity<Posting>(posting =>
            {
                posting.HasIndex(row => row.TenantId);
                if (captured is not null)
                {
                    posting.HasQueryFilter(row => row.TenantId == captured);
                }
            });
        }
    }

    private sealed class LedgerKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((Ledger)context).Tenant ?? string.Empty, designTime);
    }

    private sealed class Posting
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Memo { get; set; } = string.Empty;
    }
}
