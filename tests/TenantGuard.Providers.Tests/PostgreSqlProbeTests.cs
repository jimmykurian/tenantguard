// <copyright file="PostgreSqlProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TenantGuard.Providers.Tests;

// The provider tests on PostgreSQL 17.
public sealed class PostgreSqlProbeTests(PostgreSqlServer server) : ProviderProbeTests, IClassFixture<PostgreSqlServer>
{
    protected override DbContextOptionsBuilder UseDatabase(DbContextOptionsBuilder builder, string database)
        => builder.UseNpgsql(new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = database }.ConnectionString);
}
