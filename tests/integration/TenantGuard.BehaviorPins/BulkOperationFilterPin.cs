// <copyright file="BulkOperationFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete
[FeatureDescription("Query filters apply to ExecuteUpdate and ExecuteDelete, so a bulk statement only touches the current tenant's rows.")]
public sealed class BulkOperationFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private int _updated;
    private int _deleted;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task ExecuteUpdate_and_ExecuteDelete_touch_only_the_current_tenants_rows()
        => Runner.RunScenarioAsync(
            Given_acme_has_two_invoices_and_globex_one,
            When_globex_updates_then_deletes_every_invoice_in_bulk,
            Then_each_statement_touched_only_globexs_invoice,
            Then_acmes_invoices_are_unchanged);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_acme_has_two_invoices_and_globex_one()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        await using var setup = new PinContext(_options, "acme");
        await setup.Database.EnsureCreatedAsync(Token);
        setup.Invoices.AddRange(
            new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" },
            new Invoice { Id = 2, TenantId = "acme", Number = "acme-2" },
            new Invoice { Id = 3, TenantId = "globex", Number = "globex-1" });
        await setup.SaveChangesAsync(Token);
    }

    private async Task When_globex_updates_then_deletes_every_invoice_in_bulk()
    {
        await using var globex = new PinContext(_options, "globex");
        _updated = await globex.Invoices.ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Number, "changed"), Token);
        _deleted = await globex.Invoices.ExecuteDeleteAsync(Token);
    }

    private Task Then_each_statement_touched_only_globexs_invoice()
    {
        (_updated, _deleted).Should().Be((1, 1));
        return Task.CompletedTask;
    }

    private async Task Then_acmes_invoices_are_unchanged()
    {
        await using var acme = new PinContext(_options, "acme");
        var numbers = await acme.Invoices.OrderBy(invoice => invoice.Id).Select(invoice => invoice.Number).ToListAsync(Token);
        numbers.Should().Equal("acme-1", "acme-2");
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
