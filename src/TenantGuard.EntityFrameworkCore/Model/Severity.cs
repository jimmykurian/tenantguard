// <copyright file="Severity.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// The severity of a <see cref="ModelFinding"/>.
/// </summary>
public enum Severity
{
    /// <summary>
    /// Always logged; never stops the model from building.
    /// </summary>
    Warning,

    /// <summary>
    /// Fails the model under <see cref="Enforcement.Throw"/>, and is logged at Error level under
    /// <see cref="Enforcement.Log"/>.
    /// </summary>
    Error,
}
