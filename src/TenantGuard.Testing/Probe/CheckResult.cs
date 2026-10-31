// <copyright file="CheckResult.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

// One check for one pair of tenants.
internal readonly record struct CheckResult(ProbeOutcome Outcome, string Detail)
{
    public static CheckResult Passed => new(ProbeOutcome.Passed, string.Empty);

    public static CheckResult Failed(string detail) => new(ProbeOutcome.Failed, detail);

    public static CheckResult Skipped(string detail) => new(ProbeOutcome.Skipped, detail);
}
