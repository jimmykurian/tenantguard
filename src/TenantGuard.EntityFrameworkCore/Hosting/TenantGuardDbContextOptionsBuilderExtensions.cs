// <copyright file="TenantGuardDbContextOptionsBuilderExtensions.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard;

/// <summary>
/// Adds TenantGuard to a context's options.
/// </summary>
public static class TenantGuardDbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Checks the model against the TenantGuard rules when EF finalizes it, the first time the context's
    /// <see cref="DbContext.Model"/> is used.
    /// </summary>
    /// <param name="optionsBuilder">The context's options builder.</param>
    /// <param name="configure">Configures TenantGuard. A second call builds on the first.</param>
    /// <returns>The same options builder, for chaining.</returns>
    public static DbContextOptionsBuilder UseTenantGuard(
        this DbContextOptionsBuilder optionsBuilder,
        Action<TenantGuardOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(configure);

        var existing = optionsBuilder.Options.FindExtension<TenantGuardOptionsExtension>();
        var builder = new TenantGuardOptionsBuilder(existing?.Options ?? TenantGuardOptions.Default);
        configure(builder);

        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(
            new TenantGuardOptionsExtension(builder.Options));
        return optionsBuilder;
    }

    /// <summary>
    /// Checks the model against the TenantGuard rules when EF finalizes it, the first time the context's
    /// <see cref="DbContext.Model"/> is used.
    /// </summary>
    /// <typeparam name="TContext">The context type.</typeparam>
    /// <param name="optionsBuilder">The context's options builder.</param>
    /// <param name="configure">Configures TenantGuard. A second call builds on the first.</param>
    /// <returns>The same options builder, for chaining.</returns>
    public static DbContextOptionsBuilder<TContext> UseTenantGuard<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<TenantGuardOptionsBuilder> configure)
        where TContext : DbContext
        => (DbContextOptionsBuilder<TContext>)UseTenantGuard((DbContextOptionsBuilder)optionsBuilder, configure);
}
