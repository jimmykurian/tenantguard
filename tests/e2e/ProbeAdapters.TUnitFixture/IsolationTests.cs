// <copyright file="IsolationTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using ProbeAdapters.Store;
using TenantGuard.Testing;

namespace ProbeAdapters.TUnitFixture;

// Two tests on one source: the probe runs once, and each test gets every row.
public sealed class IsolationTests
{
    [Test]
    [ProbeRows<StoreProbe>]
    public void Tenant_isolation(ProbeRow row) => row.AssertPassed();

    [Test]
    [ProbeRows<StoreProbe>]
    public void Same_source_again(ProbeRow row)
    {
        if (StoreProbe.Runs != 1)
        {
            throw new InvalidOperationException($"The probe ran {StoreProbe.Runs} times.");
        }

        row.AssertPassed();
    }
}
