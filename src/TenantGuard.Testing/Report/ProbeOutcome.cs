// <copyright file="ProbeOutcome.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// How one check of the isolation probe came out.
/// </summary>
public enum ProbeOutcome
{
    /// <summary>
    /// The entity stayed inside its tenant.
    /// </summary>
    Passed,

    /// <summary>
    /// One tenant read, found, deleted or wrote another tenant's row.
    /// </summary>
    Failed,

    /// <summary>
    /// The check could not run; the detail says why. It is not a pass.
    /// </summary>
    Skipped,
}
