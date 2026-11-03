// <copyright file="StoreProbe.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard;
using TenantGuard.Testing;

namespace ProbeAdapters.Store;

// Probes StoreContext on an in-memory SQLite database. Under Enforcement.Log the leaky model builds,
// with TGM001 logged, so the probe can show the leak. Runs counts the runs, to show there is one.
public sealed class StoreProbe : IProbeSource
{
    private static int _runs;

    public static int Runs => _runs;

    public async Task<ProbeReport> RunAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _runs);
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<StoreContext>()
            .UseSqlite(connection)
            .UseTenantGuard(tg => tg
                .Enforcement(Enforcement.Log)
                .CurrentTenant(context => ((StoreContext)context).TenantId))
            .Options;

        await using (var context = new StoreContext(options, tenant: null))
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        // The connection stays open for the process, which holds the in-memory database.
        return await new TenantIsolationProbe<StoreContext>(tenant => new StoreContext(options, tenant))
            .WithTenants("acme", "globex")
            .RunAsync(cancellationToken);
    }
}
