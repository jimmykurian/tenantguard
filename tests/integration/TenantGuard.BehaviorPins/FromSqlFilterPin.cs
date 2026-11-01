// <copyright file="FromSqlFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/querying/sql-queries
[FeatureDescription("Query filters compose over FromSqlRaw on an entity type, so analyzer TG002 leaves FromSql, FromSqlRaw and FromSqlInterpolated alone. If this fails, they join TG002.")]
public sealed class FromSqlFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private List<string> _tenants = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task FromSqlRaw_returns_only_the_current_tenants_rows()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_an_invoice,
            When_globex_reads_every_invoice_with_FromSqlRaw,
            Then_globex_gets_only_its_own_invoice);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_acme_and_globex_each_have_an_invoice()
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

    private async Task When_globex_reads_every_invoice_with_FromSqlRaw()
    {
        await using var globex = new PinContext(_options, "globex");
        _tenants = await globex.Invoices.FromSqlRaw("SELECT * FROM Invoices").Select(invoice => invoice.TenantId).ToListAsync(Token);
    }

    private Task Then_globex_gets_only_its_own_invoice()
    {
        _tenants.Should().Equal("globex");
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().ToTable("Invoices").HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
