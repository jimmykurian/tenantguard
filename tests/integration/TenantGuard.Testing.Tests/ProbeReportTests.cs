// <copyright file="ProbeReportTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing.Tests;

// ProbeReport.ToString: the table of every row, passed ones included, under a summary
// (docs/reference/probe.md, "The report").
public sealed class ProbeReportTests
{
    [Fact]
    public void A_report_with_a_failed_row_prints_every_row_under_how_many_did_not_pass()
        => new ProbeReport(
            [
                new ProbeRow("Invoice", "ReadIsolation", ProbeOutcome.Passed, string.Empty),
                new ProbeRow("Customer", "ReadIsolation", ProbeOutcome.Failed, "As acme, Set<Customer>() returned globex's row (Id: 2)."),
            ]).ToString().Should().Be(
                "TenantGuard isolation probe: 1 of 2 checks did not pass.\n\n"
                + "Entity    Check          Outcome  Detail\n"
                + "--------  -------------  -------  ------\n"
                + "Invoice   ReadIsolation  Passed   \n"
                + "Customer  ReadIsolation  Failed   As acme, Set<Customer>() returned globex's row (Id: 2).");

    [Fact]
    public void A_report_that_passed_says_so()
        => new ProbeReport([new ProbeRow("Invoice", "ReadIsolation", ProbeOutcome.Passed, string.Empty)])
            .ToString().Should().StartWith("TenantGuard isolation probe: all 1 checks passed.\n\nEntity   Check          Outcome  Detail\n");
}
