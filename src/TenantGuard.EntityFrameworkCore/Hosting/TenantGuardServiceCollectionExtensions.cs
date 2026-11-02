// <copyright file="TenantGuardServiceCollectionExtensions.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace TenantGuard;

/// <summary>
/// Adds TenantGuard's startup validation to a service collection.
/// </summary>
public static class TenantGuardServiceCollectionExtensions
{
    /// <summary>
    /// Builds the model of <typeparamref name="TContext"/> when the host starts, so a model that breaks
    /// the TenantGuard rules stops the host before it serves a request. The check runs through
    /// <c>ValidateOnStart</c> and resolves the context, or its <see cref="IDbContextFactory{TContext}"/>, in a scope.
    /// </summary>
    /// <typeparam name="TContext">A context registered with <c>UseTenantGuard</c>.</typeparam>
    /// <param name="services">The application's services.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddTenantGuardStartupValidation<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<TenantGuardStartupCheck<TContext>>, TenantGuardStartupValidator<TContext>>());
        services.AddOptions<TenantGuardStartupCheck<TContext>>().ValidateOnStart();
        return services;
    }
}
