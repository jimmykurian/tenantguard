// <copyright file="IsolationTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using ProbeAdapters.Store;
using TenantGuard.Testing;

namespace ProbeAdapters.XunitFixture;

// Two theories on one source: the probe runs once, and each theory gets every row.
public sealed class IsolationTests
{
    [Theory]
    [ProbeRows<StoreProbe>]
    public void Tenant_isolation(ProbeRow row) => row.AssertPassed();

    [Theory]
    [ProbeRows<StoreProbe>]
    public void Same_source_again(ProbeRow row)
    {
        Assert.Equal(1, StoreProbe.Runs);
        row.AssertPassed();
    }
}
