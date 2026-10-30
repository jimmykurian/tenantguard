// <copyright file="TenantGuardScope.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;

namespace TenantGuard;

// One per context instance, in EF's internal service provider: the options that context was built
// with, its logger, and how many bypass scopes are open on it.
internal sealed class TenantGuardScope : IResettableService
{
    private TenantGuardEntities? _entities;
    private int _bypasses;

    // Bumped when a pooled context goes back to the pool, so a scope from an earlier lease ends nothing.
    private int _generation;

    public TenantGuardScope(IDbContextOptions contextOptions)
    {
        Options = contextOptions.FindExtension<TenantGuardOptionsExtension>()?.Options ?? TenantGuardOptions.Default;
        Logger = TenantGuardLogging.CreateLogger(contextOptions);
    }

    public TenantGuardOptions Options { get; }

    public ILogger Logger { get; }

    public bool IsBypassed => _bypasses > 0;

    // A context's model never changes, so its metadata is looked up once per context instance.
    public TenantGuardEntities GetEntities(DbContext context)
        => _entities ??= TenantGuardEntities.For(context.Model, Options);

    public IDisposable EnterBypass()
    {
        _bypasses++;
        return new BypassScope(this, _generation);
    }

    // A pooled context comes back with the write guard on, even if a bypass was never disposed.
    public void ResetState()
    {
        _bypasses = 0;
        _generation++;
    }

    public Task ResetStateAsync(CancellationToken cancellationToken = default)
    {
        ResetState();
        return Task.CompletedTask;
    }

    // Disposing twice ends the bypass once.
    private sealed class BypassScope(TenantGuardScope owner, int generation) : IDisposable
    {
        private TenantGuardScope? _owner = owner;

        public void Dispose()
        {
            if (_owner is { } scope && scope._generation == generation)
            {
                scope._bypasses--;
            }

            _owner = null;
        }
    }
}
