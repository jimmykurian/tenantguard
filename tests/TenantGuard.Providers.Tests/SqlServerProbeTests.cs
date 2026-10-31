// <copyright file="SqlServerProbeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Providers.Tests;

// The provider tests on SQL Server 2022.
public sealed class SqlServerProbeTests(SqlServerContainer server) : ProviderProbeTests, IClassFixture<SqlServerContainer>
{
    protected override DbContextOptionsBuilder UseDatabase(DbContextOptionsBuilder builder, string database)
        => builder.UseSqlServer(new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = database }.ConnectionString);
}
