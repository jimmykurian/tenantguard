// <copyright file="ProbeSources.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Concurrent;

namespace TenantGuard.Testing;

// Runs each probe source once per process for the test adapters, which share the report. A run
// that throws stays thrown, so every test that asks gets the same exception.
internal static class ProbeSources
{
    private static readonly ConcurrentDictionary<Type, Lazy<Task<ProbeReport>>> _runs = new();

    public static Task<ProbeReport> RunOnceAsync<TSource>(CancellationToken cancellationToken)
        where TSource : IProbeSource, new()
        => _runs.GetOrAdd(
            typeof(TSource),
            _ => new Lazy<Task<ProbeReport>>(() => new TSource().RunAsync(cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    // The test name both adapters give a row: Invoice.ReadIsolation.
    public static string TestName(ProbeRow row)
        => row.Entity + "." + row.Check;
}
