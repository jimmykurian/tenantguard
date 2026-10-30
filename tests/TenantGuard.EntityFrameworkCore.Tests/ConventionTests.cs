// <copyright file="ConventionTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Models;
using TenantGuard.EntityFrameworkCore.Tests.Support;

namespace TenantGuard.EntityFrameworkCore.Tests;

// UseTenantGuard: the convention runs the rules when EF finalizes the model (SPEC 4.2, 4.3, 11).
public sealed class ConventionTests
{
    [Fact]
    public void Throw_enforcement_lists_every_error_when_the_model_builds()
    {
        using var context = TestModels.Create<ConventionModels.TwoUnguarded>(options => options.UseTenantGuard(_ => { }));

        var exception = Assert.Throws<TenantGuardModelException>(() => context.Model);

        Assert.Equal(
            "TenantGuard found 2 model errors:\n"
                + "- TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.\n"
                + "- TGM001: Receipt has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.",
            exception.Message);
        Assert.Equal(2, exception.Report.Errors.Count);
    }

    [Fact]
    public void Log_enforcement_logs_errors_and_lets_the_model_build()
    {
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var context = TestModels.Create<Tgm001Models.Unguarded>(options => options
            .UseLoggerFactory(loggerFactory)
            .UseTenantGuard(tg => tg.Enforcement(Enforcement.Log)));

        Assert.NotNull(context.Model);

        Assert.Equal(
            [
                new LogCapture.Entry(
                    TenantGuardLogging.Category,
                    LogLevel.Information,
                    "ModelValidated",
                    "TenantGuard checked the model: 1 tenant-scoped entity types, 1 errors, 0 warnings"),
                new LogCapture.Entry(
                    TenantGuardLogging.Category,
                    LogLevel.Error,
                    "ModelFinding",
                    "TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type."),
            ],
            capture.TenantGuardEntries);
    }

    [Fact]
    public void Warnings_are_logged_under_throw_enforcement()
    {
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var context = TestModels.Create<Tgm005Models.NullableTenant>(options => options
            .UseLoggerFactory(loggerFactory)
            .UseTenantGuard(_ => { }));

        Assert.NotNull(context.Model);

        var warning = Assert.Single(capture.TenantGuardEntries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal(
            "TGM005: Receipt.TenantId is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.",
            warning.Message);
    }

    [Fact]
    public void Model_carries_the_tenant_property_of_each_scoped_entity()
    {
        using var context = TestModels.Create<Tgm001Models.Guarded>(options => options.UseTenantGuard(_ => { }));

        var entities = Assert.IsType<TenantGuardEntities>(
            context.Model.FindRuntimeAnnotationValue(TenantGuardEntities.AnnotationName));

        var invoice = context.Model.FindEntityType(typeof(Tgm001Models.Invoice))!;
        Assert.Equal("TenantId", Assert.Single(entities.TenantProperties, pair => pair.Key == invoice.Name).Value);
    }

    [Fact]
    public void Findings_are_logged_through_the_application_logger_factory()
    {
        using var capture = new LogCapture();
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(capture))
            .AddDbContext<ConventionModels.AppLogged>(options => options
                .UseSqlite("DataSource=:memory:")
                .UseTenantGuard(tg => tg.Enforcement(Enforcement.Log)));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ConventionModels.AppLogged>().Model);

        Assert.Contains(capture.TenantGuardEntries, entry => entry.Level == LogLevel.Error && entry.EventName == "ModelFinding");
    }
}
