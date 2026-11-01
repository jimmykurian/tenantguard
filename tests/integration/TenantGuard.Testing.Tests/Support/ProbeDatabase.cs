// <copyright file="ProbeDatabase.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Testing.Tests.Support;

// One in-memory SQLite database with the schema of TContext, and contexts on it for any tenant.
internal sealed class ProbeDatabase<TContext> : IAsyncDisposable
    where TContext : DbContext
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions _options;
    private readonly Func<DbContextOptions, string?, TContext> _create;

    private ProbeDatabase(SqliteConnection connection, DbContextOptions options, Func<DbContextOptions, string?, TContext> create)
    {
        _connection = connection;
        _options = options;
        _create = create;
    }

    // tenantGuard null leaves UseTenantGuard out.
    public static async Task<ProbeDatabase<TContext>> CreateAsync(
        Func<DbContextOptions, string?, TContext> create,
        Action<TenantGuardOptionsBuilder>? tenantGuard)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var builder = new DbContextOptionsBuilder().UseSqlite(connection);
        if (tenantGuard is not null)
        {
            builder.UseTenantGuard(tenantGuard);
        }

        var database = new ProbeDatabase<TContext>(connection, builder.Options, create);
        await using var context = database.Create(tenant: null);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return database;
    }

    public TContext Create(string? tenant) => _create(_options, tenant);

    public TenantIsolationProbe<TContext> Probe(params string[] tenants)
        => new TenantIsolationProbe<TContext>(Create).WithTenants(tenants.Length == 0 ? ["acme", "globex"] : tenants);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
