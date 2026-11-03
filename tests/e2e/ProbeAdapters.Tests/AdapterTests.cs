// <copyright file="AdapterTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ProbeAdapters.Tests;

// Each fixture has two test methods on the same StoreProbe. Both get every probe row as a test of its
// own; Same_source_again also fails if the probe ran more than once.
public sealed class AdapterTests
{
    private static readonly string[] _checks = ["ReadIsolation", "FindIsolation", "BulkIsolation", "UpdateGuard", "InsertGuard", "DeleteGuard"];

    private static readonly string[] _entities = ["Customer", "Invoice", "InvoiceTotal"];

    private static readonly string[] _methods = ["Tenant_isolation", "Same_source_again"];

    private static readonly string[] _leaks = ["Customer.ReadIsolation", "Customer.FindIsolation", "Customer.BulkIsolation"];

    public static TheoryData<string> Frameworks => ["xUnit v3", "TUnit"];

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task Each_row_is_a_test_named_after_its_entity_and_check(string framework)
    {
        var results = await RunAsync(framework);

        string[] expected = [.. _entities.SelectMany(entity => _checks.Select(check => $"{entity}.{check}"))];
        foreach (var method in _methods)
        {
            results.Where(result => result.Method == method).Select(result => result.Name).Should().BeEquivalentTo(expected, method);
        }
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task A_failed_row_fails_its_test_with_the_rows_table(string framework)
    {
        var results = await RunAsync(framework);

        results.Where(result => result.Outcome == "Failed").Select(result => result.Name).Distinct().Should().BeEquivalentTo(_leaks);
        var read = results.First(result => result.Name == "Customer.ReadIsolation");
        read.Message.Should().Contain("TenantGuard isolation probe: Customer ReadIsolation did not pass.")
            .And.Contain("As acme, Set<Customer>() returned globex's row (Id: 2).");
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task A_skipped_row_is_a_skipped_test_with_its_detail(string framework)
    {
        var results = await RunAsync(framework);

        var skipped = results.Where(result => result.Outcome == "Skipped").ToList();
        skipped.Select(result => result.Name).Distinct().Should().BeEquivalentTo(_checks.Select(check => $"InvoiceTotal.{check}"));
        skipped.Should().AllSatisfy(result => result.Message.Should().Contain("It is keyless, so the probe cannot write or find its rows."));
    }

    [Theory]
    [MemberData(nameof(Frameworks))]
    public async Task The_probe_runs_once_for_every_test_on_its_source(string framework)
    {
        var results = await RunAsync(framework);

        results.Where(result => result.Method == "Same_source_again" && !_leaks.Contains(result.Name) && result.Outcome != "Skipped")
            .Should().HaveCount(9).And.OnlyContain(result => result.Outcome == "Passed");
    }

    private static Task<IReadOnlyList<TestResult>> RunAsync(string framework)
        => framework == "TUnit" ? FixtureRun.TUnitAsync() : FixtureRun.XunitAsync();
}
