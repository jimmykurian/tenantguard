// <copyright file="PerTenantModel.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard;

// AllowCapturedValues: whether the model a context is building is its tenant's own, so a filter may
// capture the tenant (TGM003). The check sees only this context; the write guard and the probe see the
// others.
internal static class PerTenantModel
{
    // The tenant the model is built for, or null when TGM003 stays: the option is off, the context has
    // no current tenant, its model cache key factory is EF Core's own ModelCacheKeyFactory, which keys by
    // context type, or the key is a tuple without the tenant in it.
    public static object? TenantFor(DbContext context, TenantGuardOptions options)
    {
        if (!options.AllowCapturedValues || options.CurrentTenant is not { } resolve || resolve(context) is not { } tenant)
        {
            return null;
        }

        var factory = context.GetService<IModelCacheKeyFactory>();
        if (factory.GetType() == typeof(ModelCacheKeyFactory))
        {
            return null;
        }

        var key = factory.Create(context, designTime: false);
        return key is ITuple tuple && !Enumerable.Range(0, tuple.Length).Any(item => Equals(tuple[item], tenant))
            ? null
            : tenant;
    }
}
