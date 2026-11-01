// <copyright file="StartupValidationTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// AddTenantGuardStartupValidation: a bad model stops the host at start.
public sealed class StartupValidationTests
{
    [Fact]
    public async Task Host_start_fails_naming_the_unguarded_entity_and_its_property()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDbContext<Tgm001Models.Unguarded>(options => options
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(_ => { }));
        builder.Services.AddTenantGuardStartupValidation<Tgm001Models.Unguarded>();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<TenantGuardModelException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "TenantGuard found 1 model error:\n"
                + "- TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.",
            exception.Message);
    }

    [Fact]
    public async Task Host_starts_when_the_model_is_guarded()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDbContext<Tgm001Models.Guarded>(options => options
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(_ => { }));
        builder.Services.AddTenantGuardStartupValidation<Tgm001Models.Guarded>();
        using var host = builder.Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Host_start_builds_the_model_through_a_context_factory()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDbContextFactory<Tgm001Models.Unguarded>(options => options
            .UseSqlite("DataSource=:memory:")
            .UseTenantGuard(_ => { }));
        builder.Services.AddTenantGuardStartupValidation<Tgm001Models.Unguarded>();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<TenantGuardModelException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("TGM001", Assert.Single(exception.Report.Errors).RuleId);
    }

    [Fact]
    public async Task Host_start_fails_when_the_context_does_not_use_TenantGuard()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddDbContext<Tgm001Models.Unguarded>(options => options.UseSqlite("DataSource=:memory:"));
        builder.Services.AddTenantGuardStartupValidation<Tgm001Models.Unguarded>();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "AddTenantGuardStartupValidation<Unguarded>() needs Unguarded configured with UseTenantGuard; without it there are no rules to run.",
            Assert.Single(exception.Failures));
    }

    [Fact]
    public async Task Host_start_fails_when_the_context_is_not_registered()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddTenantGuardStartupValidation<Tgm001Models.Unguarded>();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "AddTenantGuardStartupValidation<Unguarded>() found neither Unguarded nor IDbContextFactory<Unguarded>. Register the context before the host starts.",
            Assert.Single(exception.Failures));
    }
}
