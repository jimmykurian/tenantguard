// <copyright file="ConventionTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Models;
using TenantGuard.EntityFrameworkCore.Tests.Support;

namespace TenantGuard.EntityFrameworkCore.Tests;

// UseTenantGuard: the convention runs the rules when EF finalizes the model.
public sealed class ConventionTests
{
    [Fact]
    public void Throw_enforcement_lists_every_error_when_the_model_builds()
    {
        using var context = TestModels.Create<ConventionModels.TwoUnguarded>(options => options.UseTenantGuard(_ => { }));

        var exception = FluentActions.Invoking(() => context.Model).Should().ThrowExactly<TenantGuardModelException>().Which;

        exception.Message.Should().Be("TenantGuard found 2 model errors:\n"
                + "- TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.\n"
                + "- TGM001: Receipt has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.");
        exception.Report.Errors.Count.Should().Be(2);
    }

    [Fact]
    public void Log_enforcement_logs_errors_and_lets_the_model_build()
    {
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var context = TestModels.Create<Tgm001Models.Unguarded>(options => options
            .UseLoggerFactory(loggerFactory)
            .UseTenantGuard(tg => tg.Enforcement(Enforcement.Log)));

        context.Model.Should().NotBeNull();

        capture.TenantGuardEntries.Should().Equal([
                new LogCapture.Entry(
                    TenantGuardLogging.Category,
                    LogLevel.Information,
                    "ModelValidated",
                    "TenantGuard checked the model: 1 tenant-scoped entity types, 1 errors, 1 warnings"),
                new LogCapture.Entry(
                    TenantGuardLogging.Category,
                    LogLevel.Warning,
                    "ModelFinding",
                    "TGM009: Invoice has no index or key that starts with 'TenantId', so each query its tenant filter scopes reads past other tenants' rows. Add HasIndex(e => e.TenantId), or put 'TenantId' first in an existing index or key."),
                new LogCapture.Entry(
                    TenantGuardLogging.Category,
                    LogLevel.Error,
                    "ModelFinding",
                    "TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type."),
            ]);
    }

    [Fact]
    public void Warnings_are_logged_under_throw_enforcement()
    {
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var context = TestModels.Create<Tgm005Models.NullableTenant>(options => options
            .UseLoggerFactory(loggerFactory)
            .UseTenantGuard(_ => { }));

        context.Model.Should().NotBeNull();

        capture.TenantGuardEntries.Where(entry => entry.Level == LogLevel.Warning).Select(entry => entry.Message).Should().Contain(
            "TGM005: Receipt.TenantId is nullable; rows with no tenant are invisible to every tenant and bypass the write guard.");
    }

    [Fact]
    public void Model_carries_the_tenant_property_of_each_scoped_entity()
    {
        using var context = TestModels.Create<Tgm001Models.Guarded>(options => options.UseTenantGuard(_ => { }));

        var entities = context.Model.FindRuntimeAnnotationValue(TenantGuardEntities.AnnotationName).Should().BeOfType<TenantGuardEntities>().Subject;

        var invoice = context.Model.FindEntityType(typeof(Tgm001Models.Invoice))!;
        entities.TenantProperties.Should().ContainSingle(pair => pair.Key == invoice.Name).Subject.Value.Should().Be("TenantId");
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

        scope.ServiceProvider.GetRequiredService<ConventionModels.AppLogged>().Model.Should().NotBeNull();

        capture.TenantGuardEntries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.EventName == "ModelFinding");
    }
}
