// <copyright file="TenantKeyedModelCachePin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/modeling/dynamic-model
[FeatureDescription("An IModelCacheKeyFactory whose key includes the tenant gives each tenant's context a model of its own, so a filter that captures the tenant returns only that tenant's rows. AllowCapturedValues rests on it.")]
public sealed class TenantKeyedModelCachePin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private List<string> _visible = [];
    private bool _sameModel;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Each_tenant_gets_its_own_model_and_sees_only_its_rows()
        => Runner.RunScenarioAsync(
            Given_a_model_cache_keyed_by_tenant_and_a_row_for_each_tenant,
            When_a_context_for_globex_reads_the_invoices,
            Then_globex_has_a_model_of_its_own,
            Then_globex_sees_only_its_invoice);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_a_model_cache_keyed_by_tenant_and_a_row_for_each_tenant()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCacheKeyFactory, TenantKeyFactory>()
            .Options;
        await using var acme = new PinContext(_options, "acme");
        await acme.Database.EnsureCreatedAsync(Token);
        acme.Invoices.AddRange(
            new Invoice { TenantId = "acme", Number = "acme-1" },
            new Invoice { TenantId = "globex", Number = "globex-1" });
        await acme.SaveChangesAsync(Token);
    }

    private async Task When_a_context_for_globex_reads_the_invoices()
    {
        await using var acme = new PinContext(_options, "acme");
        await using var globex = new PinContext(_options, "globex");
        _sameModel = ReferenceEquals(acme.Model, globex.Model);
        _visible = await globex.Invoices.Select(invoice => invoice.TenantId).ToListAsync(Token);
    }

    private Task Then_globex_has_a_model_of_its_own()
    {
        _sameModel.Should().BeFalse();
        return Task.CompletedTask;
    }

    private Task Then_globex_sees_only_its_invoice()
    {
        _visible.Should().ContainSingle().Subject.Should().Be("globex");
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        public string Tenant { get; } = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var captured = Tenant;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == captured);
        }
    }

    private sealed class TenantKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (context.GetType(), ((PinContext)context).Tenant, designTime);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
