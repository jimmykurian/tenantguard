// <copyright file="TenantIsolationProbeException.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// Thrown by <see cref="ProbeReport.AssertAllPassed"/>. The message is a fixed-width table of the
/// checks that failed or were skipped.
/// </summary>
public sealed class TenantIsolationProbeException : Exception
{
    internal TenantIsolationProbeException(string message)
        : base(message)
    {
    }
}
