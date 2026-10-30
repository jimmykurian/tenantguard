// <copyright file="TenantGuardSaveChangesInterceptor.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TenantGuard;

// Registered by UseTenantGuard, one per context instance (SPEC 5). Both paths run the same check,
// which is synchronous because it does no I/O.
internal sealed class TenantGuardSaveChangesInterceptor(TenantGuardScope scope) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        TenantWriteGuard.Check(eventData.Context, scope);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        TenantWriteGuard.Check(eventData.Context, scope);
        return ValueTask.FromResult(result);
    }
}
