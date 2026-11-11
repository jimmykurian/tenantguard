// <copyright file="UnnamedFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/querying/filters
[FeatureDescription("IgnoreQueryFilters with names never turns an unnamed filter off, and EF Core refuses an unnamed filter beside named ones, so analyzer TG001 leaves the named overload alone under tenantguard.tenant_filter_unnamed. If this fails, the key goes.")]
public sealed class UnnamedFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private List<string> _named = [];
    private List<string> _empty = [];
    private Exception? _mixed;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task IgnoreQueryFilters_with_names_leaves_an_unnamed_filter_on()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_an_invoice_behind_an_unnamed_filter,
            When_globex_ignores_filters_by_name,
            Then_globex_still_gets_only_its_own_invoice);

    [Fact]
    public Task An_unnamed_filter_cannot_sit_beside_a_named_one()
        => Runner.RunScenarioAsync(
            When_a_model_has_an_unnamed_and_a_named_filter_on_one_entity_type,
            Then_building_it_throws);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_acme_and_globex_each_have_an_invoice_behind_an_unnamed_filter()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        await using var setup = new PinContext(_options, "acme");
        await setup.Database.EnsureCreatedAsync(Token);
        setup.Invoices.AddRange(
            new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" },
            new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });
        await setup.SaveChangesAsync(Token);
    }

    private async Task When_globex_ignores_filters_by_name()
    {
        await using var globex = new PinContext(_options, "globex");
        _named = await globex.Invoices.IgnoreQueryFilters(["SoftDelete"]).Select(invoice => invoice.TenantId).ToListAsync(Token);
        _empty = await globex.Invoices.IgnoreQueryFilters([]).Select(invoice => invoice.TenantId).ToListAsync(Token);
    }

    private Task Then_globex_still_gets_only_its_own_invoice()
    {
        _named.Should().Equal("globex");
        _empty.Should().Equal("globex");
        return Task.CompletedTask;
    }

    private async Task When_a_model_has_an_unnamed_and_a_named_filter_on_one_entity_type()
    {
        await _connection.OpenAsync(Token);
        var options = new DbContextOptionsBuilder<MixedContext>().UseSqlite(_connection).Options;
        await using var context = new MixedContext(options);
        _mixed = Record.Exception(() => context.Model);
    }

    private Task Then_building_it_throws()
    {
        _mixed.Should().BeOfType<InvalidOperationException>();
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class MixedContext(DbContextOptions<MixedContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>()
                .HasQueryFilter(invoice => invoice.TenantId == "acme")
                .HasQueryFilter("SoftDelete", invoice => invoice.Number != string.Empty);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
