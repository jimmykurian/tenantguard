// <copyright file="ProbeRowsAttribute.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using Xunit;
using Xunit.Sdk;
using Xunit.v3;

namespace TenantGuard.Testing;

/// <summary>
/// Gives an xUnit v3 theory one test for each row of the probe <typeparamref name="TSource"/> runs,
/// named <c>{Entity}.{Check}</c>. A skipped row is a skipped test. The probe runs once per test process,
/// when the theory runs.
/// </summary>
/// <typeparam name="TSource">The class that runs the probe.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProbeRowsAttribute<TSource> : DataAttribute
    where TSource : IProbeSource, new()
{
    /// <inheritdoc/>
    public override async ValueTask<IReadOnlyCollection<ITheoryDataRow>> GetData(MethodInfo testMethod, DisposalTracker disposalTracker)
    {
        var report = await ProbeSources.RunOnceAsync<TSource>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return [.. report.Rows.Select(ToDataRow)];
    }

    /// <summary>
    /// Returns false: the rows come from running the probe, so xUnit asks for them when the theory runs.
    /// </summary>
    /// <returns>False.</returns>
    public override bool SupportsDiscoveryEnumeration() => false;

    private static TheoryDataRow ToDataRow(ProbeRow row)
        => new(row)
        {
            TestDisplayName = ProbeSources.TestName(row),
            Label = string.Empty,
            Skip = row.Outcome == ProbeOutcome.Skipped ? row.Detail : null,
        };
}
