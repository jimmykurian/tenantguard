// <copyright file="TenantGuardStartupCheck.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard;

// The options type ValidateOnStart validates, one per context type. It carries no settings; the
// options system creates it, and TenantGuardStartupValidator does the work.
internal sealed class TenantGuardStartupCheck<TContext>
    where TContext : DbContext
{
}
