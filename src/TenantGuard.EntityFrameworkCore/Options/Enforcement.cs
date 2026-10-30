// <copyright file="Enforcement.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// Says what TenantGuard does when the model breaks a rule with <see cref="Severity.Error"/> severity.
/// </summary>
public enum Enforcement
{
    /// <summary>
    /// Throw a <see cref="TenantGuardModelException"/> that lists every error.
    /// </summary>
    Throw,

    /// <summary>
    /// Log every error at Error level and let the model build.
    /// </summary>
    Log,
}
