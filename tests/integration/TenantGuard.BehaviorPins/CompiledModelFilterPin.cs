// <copyright file="CompiledModelFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.DependencyInjection;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/performance/advanced-performance-topics#compiled-models
[FeatureDescription("EF Core can't generate a compiled model for a model with a query filter, so an app that isolates tenants with query filters can't use a compiled model or Native AOT. If this fails, revisit decision 0027.")]
public sealed class CompiledModelFilterPin : FeatureFixture, IDisposable
{
    private readonly string _outputDirectory = Directory.CreateTempSubdirectory("tenantguard-pin-").FullName;
    private Action _scaffold = null!;

    [Fact]
    public Task A_compiled_model_with_a_query_filter_is_refused()
        => Runner.RunScenarioAsync(
            Given_a_model_with_a_tenant_query_filter,
            Then_generating_its_compiled_model_is_refused);

    public void Dispose() => Directory.Delete(_outputDirectory, recursive: true);

    private Task Given_a_model_with_a_tenant_query_filter()
    {
        var context = new PinContext(new DbContextOptionsBuilder<PinContext>().UseSqlite("DataSource=:memory:").Options);
        var assembly = typeof(PinContext).Assembly;
#pragma warning disable EF1001 // DesignTimeServicesBuilder is what dotnet ef dbcontext optimize uses.
        var services = new DesignTimeServicesBuilder(assembly, assembly, new OperationReporter(handler: null), [])
            .Build(context);
#pragma warning restore EF1001
        var scaffolder = services.GetRequiredService<ICompiledModelScaffolder>();
        var model = context.GetService<IDesignTimeModel>().Model;
        _scaffold = () => scaffolder.ScaffoldModel(
            model,
            _outputDirectory,
            new CompiledModelCodeGenerationOptions
            {
                ContextType = typeof(PinContext),
                ModelNamespace = "TenantGuard.BehaviorPins.Compiled",
                Language = "C#",
            });
        return Task.CompletedTask;
    }

    private Task Then_generating_its_compiled_model_is_refused()
    {
        _scaffold.Should().Throw<InvalidOperationException>().WithMessage("*query filter*");
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options) : DbContext(options)
    {
        public string TenantId { get; set; } = "acme";

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }
    }
}
