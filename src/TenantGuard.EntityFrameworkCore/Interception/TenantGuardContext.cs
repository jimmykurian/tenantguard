// <copyright file="TenantGuardContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// TenantGuard's controls for one context instance, from
/// <see cref="TenantGuardDbContextExtensions.TenantGuard(Microsoft.EntityFrameworkCore.DbContext)"/>.
/// </summary>
public sealed class TenantGuardContext
{
    private readonly TenantGuardScope _scope;
    private readonly string _contextName;

    internal TenantGuardContext(TenantGuardScope scope, string contextName)
    {
        _scope = scope;
        _contextName = contextName;
    }

    /// <summary>
    /// Turns off the write guard for this context instance until the returned scope is disposed, and
    /// logs a warning with the reason. Query filters stay on.
    /// </summary>
    /// <param name="reason">Why the writes cross tenants, such as "nightly aggregation across tenants".</param>
    /// <returns>A scope; disposing it turns the write guard back on.</returns>
    public IDisposable Bypass(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        TenantGuardLog.Bypass(_scope.Logger, reason, _contextName);
        return _scope.EnterBypass();
    }
}
