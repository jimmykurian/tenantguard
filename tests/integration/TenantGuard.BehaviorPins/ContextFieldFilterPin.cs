// <copyright file="ContextFieldFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/querying/filters
[FeatureDescription("A filter that references a field of the context is evaluated per context instance, so two contexts for two tenants each see only their own rows. TGM003 relies on this being the safe pattern.")]
public sealed class ContextFieldFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private Func<string, DbContext> _create = null!;
    private List<string> _acmeSees = [];
    private List<string> _globexSees = [];
    private ModelReport _report = null!;

    private interface ITenantSource
    {
        string TenantId { get; }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Each_context_sees_only_its_own_tenants_rows()
        => Runner.RunScenarioAsync(
            Given_a_filter_that_reads_a_field_of_the_context,
            Given_acme_and_globex_each_have_an_invoice,
            When_a_context_for_each_tenant_reads_the_invoices,
            Then_each_tenant_sees_only_its_own_invoice,
            Then_TenantGuard_finds_nothing_wrong_with_the_model);

    // Finbuckle's pattern: the filter reads a captured null of an interface the context implements,
    // and EF puts the current context in its place, so TGM003 reads it as the context.
    [Fact]
    public Task Captured_interface_the_context_implements_is_read_from_each_context()
        => Runner.RunScenarioAsync(
            Given_a_filter_that_reads_a_captured_interface_the_context_implements,
            Given_acme_and_globex_each_have_an_invoice,
            When_a_context_for_each_tenant_reads_the_invoices,
            Then_each_tenant_sees_only_its_own_invoice,
            Then_TenantGuard_finds_nothing_wrong_with_the_model);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private Task Given_a_filter_that_reads_a_field_of_the_context()
    {
        var options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        _create = tenant => new PinContext(options, tenant);
        return Task.CompletedTask;
    }

    private Task Given_a_filter_that_reads_a_captured_interface_the_context_implements()
    {
        var options = new DbContextOptionsBuilder<InterfaceContext>().UseSqlite(_connection).Options;
        _create = tenant => new InterfaceContext(options, tenant);
        return Task.CompletedTask;
    }

    private async Task Given_acme_and_globex_each_have_an_invoice()
    {
        await _connection.OpenAsync(Token);
        await using var setup = _create("acme");
        await setup.Database.EnsureCreatedAsync(Token);
        setup.Set<Invoice>().AddRange(
            new Invoice { TenantId = "acme", Number = "acme-1" },
            new Invoice { TenantId = "globex", Number = "globex-1" });
        await setup.SaveChangesAsync(Token);
    }

    private async Task When_a_context_for_each_tenant_reads_the_invoices()
    {
        await using var acme = _create("acme");
        await using var globex = _create("globex");
        _acmeSees = await acme.Set<Invoice>().Select(invoice => invoice.TenantId).ToListAsync(Token);
        _globexSees = await globex.Set<Invoice>().Select(invoice => invoice.TenantId).ToListAsync(Token);
        _report = TenantGuardModel.Validate(globex.Model, new TenantGuardOptionsBuilder().Options);
    }

    private Task Then_each_tenant_sees_only_its_own_invoice()
    {
        _acmeSees.Should().ContainSingle().Subject.Should().Be("acme");
        _globexSees.Should().ContainSingle().Subject.Should().Be("globex");
        return Task.CompletedTask;
    }

    private Task Then_TenantGuard_finds_nothing_wrong_with_the_model()
    {
        _report.Findings.Should().BeEmpty();
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == _tenant).HasIndex(invoice => invoice.TenantId);
    }

    private sealed class InterfaceContext(DbContextOptions<InterfaceContext> options, string tenant) : DbContext(options), ITenantSource
    {
        public string TenantId { get; } = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ITenantSource? context = null;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == context!.TenantId).HasIndex(invoice => invoice.TenantId);
        }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
