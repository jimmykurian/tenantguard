// <copyright file="CapturedLocalFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/querying/filters
[FeatureDescription("A filter that captures a local in OnModelCreating freezes the first tenant into the model, and EF caches the model per context type, so a context built for another tenant still sees the first tenant's rows. This is the leak TGM003 exists for.")]
public sealed class CapturedLocalFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private List<string> _visible = [];
    private ModelReport _report = null!;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Second_tenant_sees_the_first_tenants_rows_and_TGM003_flags_the_model()
        => Runner.RunScenarioAsync(
            Given_a_model_built_for_acme_with_a_captured_tenant,
            When_a_context_for_globex_reads_the_invoices,
            Then_globex_sees_acmes_invoice,
            Then_TGM003_flags_the_model);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_a_model_built_for_acme_with_a_captured_tenant()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        await using var acme = new PinContext(_options, "acme");
        await acme.Database.EnsureCreatedAsync(Token);
        acme.Invoices.AddRange(
            new Invoice { TenantId = "acme", Number = "acme-1" },
            new Invoice { TenantId = "globex", Number = "globex-1" });
        await acme.SaveChangesAsync(Token);
    }

    private async Task When_a_context_for_globex_reads_the_invoices()
    {
        await using var globex = new PinContext(_options, "globex");
        _visible = await globex.Invoices.Select(invoice => invoice.TenantId).ToListAsync(Token);
        _report = TenantGuardModel.Validate(globex.Model, new TenantGuardOptionsBuilder().Options);
    }

    private Task Then_globex_sees_acmes_invoice()
    {
        _visible.Should().ContainSingle().Subject.Should().Be("acme");
        return Task.CompletedTask;
    }

    private Task Then_TGM003_flags_the_model()
    {
        _report.Errors.Should().ContainSingle().Subject.RuleId.Should().Be("TGM003");
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var captured = tenant;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == captured);
        }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
