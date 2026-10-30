// <copyright file="NoTenantPolicy.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// Says what the write guard does when <c>CurrentTenant</c> returns null.
/// </summary>
public enum NoTenantPolicy
{
    /// <summary>
    /// Every write to a tenant-scoped entity is a violation.
    /// </summary>
    Reject,

    /// <summary>
    /// The write guard skips its checks, and logs once per <c>SaveChanges</c>.
    /// </summary>
    Allow,
}
