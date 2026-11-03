// <copyright file="IProbeSource.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// Runs an isolation probe for a test adapter, such as <c>ProbeRowsAttribute&lt;TSource&gt;</c> in
/// TenantGuard.Testing.XunitV3 or TenantGuard.Testing.TUnit. The adapter creates it with its
/// parameterless constructor, and runs it once per test process.
/// </summary>
public interface IProbeSource
{
    /// <summary>
    /// Sets up what the probe needs, runs it and returns its report.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report, whose rows become the tests.</returns>
    Task<ProbeReport> RunAsync(CancellationToken cancellationToken);
}
