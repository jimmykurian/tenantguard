// <copyright file="TenantIsolationProbe.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Testing;

/// <summary>
/// Proves, for every tenant-scoped entity type of a context, that one tenant cannot read, find, bulk
/// delete, update, insert or delete another tenant's rows, or load them through a navigation, and, with
/// <see cref="WithoutTenant"/>, that a context with no tenant reads none of them. It seeds a row per
/// tenant, then runs the checks with a fresh context per tenant per check.
/// </summary>
/// <typeparam name="TContext">The application's context type.</typeparam>
public sealed class TenantIsolationProbe<TContext>
    where TContext : DbContext
{
    private readonly Func<string, TContext> _createContext;
    private readonly Dictionary<Type, Func<string, TContext, object>> _seeders = [];
    private string[] _tenants = [];
    private Func<TContext>? _createContextWithoutTenant;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantIsolationProbe{TContext}"/> class.
    /// </summary>
    /// <param name="createContext">
    /// Creates a context that operates as the given tenant. Every context it creates has to use the
    /// same database, with the schema in place. The probe disposes each one.
    /// </param>
    public TenantIsolationProbe(Func<string, TContext> createContext)
    {
        ArgumentNullException.ThrowIfNull(createContext);

        _createContext = createContext;
    }

    /// <summary>
    /// Sets the tenants to seed and check against each other: at least two, in a form the tenant
    /// property can hold, such as "acme" for a string or a Guid's text for a Guid.
    /// </summary>
    /// <param name="tenants">The tenants.</param>
    /// <returns>The same probe, for chaining.</returns>
    public TenantIsolationProbe<TContext> WithTenants(params string[] tenants)
    {
        ArgumentNullException.ThrowIfNull(tenants);
        if (tenants.Length < 2 || tenants.Any(string.IsNullOrWhiteSpace) || tenants.Distinct(StringComparer.Ordinal).Count() != tenants.Length)
        {
            throw new ArgumentException("Pass at least two tenants, distinct and not empty.", nameof(tenants));
        }

        _tenants = [.. tenants];
        return this;
    }

    /// <summary>
    /// Adds the NoTenantIsolation check: as a context with no current tenant, every probed entity type
    /// returns none of the tenants' rows from <c>Set&lt;TEntity&gt;()</c> or <c>FindAsync</c>. A second
    /// call replaces the first factory.
    /// </summary>
    /// <param name="createContext">
    /// Creates a context with no current tenant, on the same database as the other contexts. The probe
    /// disposes each one and never writes through it.
    /// </param>
    /// <returns>The same probe, for chaining.</returns>
    public TenantIsolationProbe<TContext> WithoutTenant(Func<TContext> createContext)
    {
        ArgumentNullException.ThrowIfNull(createContext);

        _createContextWithoutTenant = createContext;
        return this;
    }

    /// <summary>
    /// Creates the rows of <typeparamref name="TEntity"/> with a function instead of the default
    /// seeder. The probe still sets the tenant property, and any required foreign key left unset.
    /// </summary>
    /// <typeparam name="TEntity">An entity type of the context.</typeparam>
    /// <param name="seed">Returns a new row for the tenant; the context is that tenant's.</param>
    /// <returns>The same probe, for chaining.</returns>
    public TenantIsolationProbe<TContext> Seed<TEntity>(Func<string, TContext, TEntity> seed)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(seed);

        _seeders[typeof(TEntity)] = (tenant, context) => seed(tenant, context);
        return this;
    }

    /// <summary>
    /// Seeds the rows and runs the checks. A model that breaks a TenantGuard rule under
    /// <see cref="Enforcement.Throw"/> throws before anything is seeded.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A row per entity type and check.</returns>
    public Task<ProbeReport> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_tenants.Length < 2)
        {
            throw new InvalidOperationException("Call WithTenants with at least two tenants before RunAsync.");
        }

        return new ProbeRun<TContext>(_createContext, _tenants, _seeders, _createContextWithoutTenant).RunAsync(cancellationToken);
    }
}
