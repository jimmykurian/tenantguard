// <copyright file="TenantGuardStartupValidator.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace TenantGuard;

// Runs at host start through ValidateOnStart. Building the model runs the TenantGuard convention,
// whose TenantGuardModelException propagates out of the host's StartAsync unchanged.
internal sealed class TenantGuardStartupValidator<TContext>(IServiceScopeFactory scopeFactory)
    : IValidateOptions<TenantGuardStartupCheck<TContext>>
    where TContext : DbContext
{
    public ValidateOptionsResult Validate(string? name, TenantGuardStartupCheck<TContext> options)
    {
        var contextName = typeof(TContext).Name;
        using var scope = scopeFactory.CreateScope();
        if (scope.ServiceProvider.GetService<TContext>() is { } context)
        {
            return Check(context, contextName);
        }

        if (scope.ServiceProvider.GetService<IDbContextFactory<TContext>>() is { } factory)
        {
            using var created = factory.CreateDbContext();
            return Check(created, contextName);
        }

        return ValidateOptionsResult.Fail(
            $"AddTenantGuardStartupValidation<{contextName}>() found neither {contextName} nor IDbContextFactory<{contextName}>. Register the context before the host starts.");
    }

    private static ValidateOptionsResult Check(DbContext context, string contextName)
    {
        if (context.GetService<IDbContextOptions>().FindExtension<TenantGuardOptionsExtension>() is null)
        {
            return ValidateOptionsResult.Fail(
                $"AddTenantGuardStartupValidation<{contextName}>() needs {contextName} configured with UseTenantGuard; without it there are no rules to run.");
        }

        _ = context.Model;
        return ValidateOptionsResult.Success;
    }
}
