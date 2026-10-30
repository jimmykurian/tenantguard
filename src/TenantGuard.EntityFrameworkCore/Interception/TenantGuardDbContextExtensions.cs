// <copyright file="TenantGuardDbContextExtensions.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace TenantGuard;

/// <summary>
/// Reaches TenantGuard from a context.
/// </summary>
public static class TenantGuardDbContextExtensions
{
    /// <summary>
    /// Gets TenantGuard's controls for this context instance, such as <see cref="TenantGuardContext.Bypass"/>.
    /// </summary>
    /// <param name="context">A context configured with <c>UseTenantGuard</c>.</param>
    /// <returns>The controls for this context instance.</returns>
    public static TenantGuardContext TenantGuard(this DbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var contextName = context.GetType().Name;
        var scope = context.GetInfrastructure().GetService<TenantGuardScope>()
            ?? throw new InvalidOperationException($"{contextName} does not use TenantGuard. Add UseTenantGuard to its options.");
        return new TenantGuardContext(scope, contextName);
    }
}
