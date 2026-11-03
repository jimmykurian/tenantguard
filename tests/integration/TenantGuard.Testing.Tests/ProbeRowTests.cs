// <copyright file="ProbeRowTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing.Tests;

// ProbeRow.AssertPassed, which a test adapter's test calls on its row.
public sealed class ProbeRowTests
{
    [Fact]
    public void A_passed_row_does_not_throw()
        => new ProbeRow("Invoice", "ReadIsolation", ProbeOutcome.Passed, string.Empty).AssertPassed();

    [Fact]
    public void A_failed_row_throws_a_table_of_itself()
        => FluentActions.Invoking(new ProbeRow("Invoice", "ReadIsolation", ProbeOutcome.Failed, "As acme, Set<Invoice>() returned globex's row (Id: 2).").AssertPassed)
            .Should().ThrowExactly<TenantIsolationProbeException>().WithMessage(
                "TenantGuard isolation probe: Invoice ReadIsolation did not pass.\n\n"
                + "Entity   Check          Outcome  Detail\n"
                + "-------  -------------  -------  ------\n"
                + "Invoice  ReadIsolation  Failed   As acme, Set<Invoice>() returned globex's row (Id: 2).");

    [Fact]
    public void A_skipped_row_throws_too()
        => FluentActions.Invoking(new ProbeRow("InvoiceTotal", "ReadIsolation", ProbeOutcome.Skipped, "It is keyless, so the probe cannot write or find its rows.").AssertPassed)
            .Should().ThrowExactly<TenantIsolationProbeException>().WithMessage("TenantGuard isolation probe: InvoiceTotal ReadIsolation did not pass.*");
}
