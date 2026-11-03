// <copyright file="ProbeRowsAttribute.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// Gives a TUnit test one case for each row of the probe <typeparamref name="TSource"/> runs, named
/// <c>{Entity}.{Check}</c>. A skipped row is a skipped test. The probe runs once per test process,
/// when TUnit builds its tests.
/// </summary>
/// <typeparam name="TSource">The class that runs the probe.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ProbeRowsAttribute<TSource> : AsyncUntypedDataSourceGeneratorAttribute
    where TSource : IProbeSource, new()
{
    /// <inheritdoc/>
    protected override async IAsyncEnumerable<Func<Task<object?[]?>>> GenerateDataSourcesAsync(
        DataGeneratorMetadata dataGeneratorMetadata)
    {
        var report = await ProbeSources.RunOnceAsync<TSource>(CancellationToken.None).ConfigureAwait(false);
        foreach (var row in report.Rows)
        {
            // TUnit takes the test's name and skip reason from a TestDataRow it is handed.
            var named = new TestDataRow<ProbeRow>(
                row,
                DisplayName: ProbeSources.TestName(row),
                Skip: row.Outcome == ProbeOutcome.Skipped ? row.Detail : null);
            object?[] data = [named];
            yield return () => Task.FromResult<object?[]?>(data);
        }
    }
}
