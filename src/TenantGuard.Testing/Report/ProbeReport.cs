// <copyright file="ProbeReport.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// What <see cref="TenantIsolationProbe{TContext}.RunAsync"/> found: one row per entity type and check.
/// </summary>
public sealed class ProbeReport
{
    internal ProbeReport(IReadOnlyList<ProbeRow> rows)
    {
        Rows = rows;
    }

    /// <summary>
    /// Gets every row, by entity type and then in check order.
    /// </summary>
    public IReadOnlyList<ProbeRow> Rows { get; }

    /// <summary>
    /// Returns a table of every row, passed ones included, under a summary, the way
    /// <see cref="AssertAllPassed"/> shows the rows that didn't pass.
    /// </summary>
    /// <returns>The table.</returns>
    public override string ToString()
    {
        var notPassed = Rows.Count(row => row.Outcome != ProbeOutcome.Passed);
        return ProbeTable.Format(Rows, notPassed > 0 ? $"{notPassed} of {Rows.Count} checks did not pass." : $"all {Rows.Count} checks passed.");
    }

    /// <summary>
    /// Throws <see cref="TenantIsolationProbeException"/>, with a table of the rows that failed or were
    /// skipped, unless every row passed.
    /// </summary>
    public void AssertAllPassed()
    {
        var notPassed = Rows.Where(row => row.Outcome != ProbeOutcome.Passed).ToList();
        if (notPassed.Count > 0)
        {
            throw new TenantIsolationProbeException(ProbeTable.Format(notPassed, $"{notPassed.Count} of {Rows.Count} checks did not pass."));
        }
    }
}
