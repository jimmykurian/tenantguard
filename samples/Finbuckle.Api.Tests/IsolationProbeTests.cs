// <copyright file="IsolationProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard.Testing;

namespace Finbuckle.Api.Tests;

// The isolation probe against the app's Finbuckle model and its TenantGuard registration.
public sealed class IsolationProbeTests
{
    [Fact]
    public async Task Customers_and_invoices_stay_inside_their_tenant()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(connection).Options;
        await using (var setup = new AppDbContext(new FixedTenant(tenant: null), options))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
        }

        var report = await new TenantIsolationProbe<AppDbContext>(tenant => new AppDbContext(new FixedTenant(tenant), options))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);

        report.AssertAllPassed();
    }
}
