// <copyright file="RawSqlCallsTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Tg002 = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.RawSqlWithoutTenantAnalyzer>;
using Tg004 = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.UnverifiableRawSqlAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// TG002 and TG004 on the calls beyond EF Core that take raw SQL: Dapper, ADO.NET and
// tenantguard.raw_sql_methods.
public sealed class RawSqlCallsTests
{
    // Dapper's SqlMapper as far as these tests need it. The analyzers match it by name.
    private const string Dapper = """

        namespace Dapper
        {
            using System.Collections.Generic;
            using System.Data;
            using System.Threading.Tasks;

            public struct CommandDefinition
            {
                public CommandDefinition(string commandText) => CommandText = commandText;

                public string CommandText { get; }
            }

            public static class SqlMapper
            {
                public static IEnumerable<T> Query<T>(this IDbConnection cnn, string sql, object? param = null) => [];

                public static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection cnn, string sql, object? param = null) => Task.FromResult<IEnumerable<T>>([]);

                public static Task<IEnumerable<T>> QueryAsync<T>(this IDbConnection cnn, CommandDefinition command) => Task.FromResult<IEnumerable<T>>([]);

                public static T QuerySingle<T>(this IDbConnection cnn, string sql, object? param = null) => default!;

                public static int Execute(this IDbConnection cnn, string sql, object? param = null) => 0;

                public static Task<int> ExecuteAsync(this IDbConnection cnn, string sql, object? param = null) => Task.FromResult(0);

                public static object? ExecuteScalar(this IDbConnection cnn, string sql, object? param = null) => null;

                public static int Execute(this IDbConnection cnn, CommandDefinition command) => 0;
            }
        }
        """;

    [Fact]
    public Task Dapper_SQL_without_the_tenant_column_is_TG002()
        => Tg002.VerifyAsync("""
            public static class Reports
            {
                public static IEnumerable<Invoice> Unpaid(System.Data.IDbConnection connection)
                    => Dapper.SqlMapper.Query<Invoice>(connection, {|TG002:"SELECT * FROM Invoices WHERE Paid = 0"|});

                public static Task<IEnumerable<Invoice>> UnpaidAsync(System.Data.IDbConnection connection)
                    => Dapper.SqlMapper.QueryAsync<Invoice>(connection, {|TG002:"SELECT * FROM Invoices WHERE Paid = 0"|});

                public static int Purge(System.Data.IDbConnection connection)
                    => Dapper.SqlMapper.Execute(connection, param: null, sql: {|TG002:"DELETE FROM Invoices"|});
            }
            """ + Dapper);

    [Fact]
    public Task Dapper_called_as_an_extension_method_is_checked()
        => Tg002.VerifyAsync("""
            namespace App
            {
                using System.Collections.Generic;
                using System.Data;
                using System.Threading.Tasks;
                using Dapper;

                public static class Reports
                {
                    public static Task<int> PurgeAsync(IDbConnection connection) => connection.ExecuteAsync({|TG002:"DELETE FROM Invoices"|});

                    public static IEnumerable<Invoice> Mine(IDbConnection connection, string tenantId)
                        => connection.Query<Invoice>("SELECT * FROM Invoices WHERE TenantId = @tenantId", new { tenantId });

                    public static Invoice One(IDbConnection connection, string tenantId)
                        => connection.QuerySingle<Invoice>($"SELECT * FROM Invoices WHERE {nameof(Invoice.TenantId)} = @tenantId", new { tenantId });

                    public static object? Count(IDbConnection connection) => connection.ExecuteScalar({|TG002:"SELECT COUNT(*) FROM Invoices"|});
                }
            }
            """ + Dapper);

    [Fact]
    public Task Dapper_CommandDefinition_overloads_are_not_checked()
        => Tg002.VerifyAsync("""
            namespace App
            {
                using System.Data;
                using Dapper;

                public static class Reports
                {
                    public static int Purge(IDbConnection connection) => connection.Execute(new CommandDefinition("DELETE FROM Invoices"));
                }
            }
            """ + Dapper);

    [Fact]
    public Task Dapper_SQL_built_at_runtime_is_TG004()
        => Tg004.VerifyAsync("""
            namespace App
            {
                using System.Data;
                using Dapper;

                public static class Reports
                {
                    public static int Purge(IDbConnection connection, string table) => connection.Execute({|TG004:"DELETE FROM " + table|});

                    public static int Fixed(IDbConnection connection) => connection.Execute("DELETE FROM Invoices");
                }
            }
            """ + Dapper);

    [Fact]
    public Task CommandText_assignments_are_checked()
        => Tg002.VerifyAsync("""
            public static class Jobs
            {
                public static void Purge(System.Data.Common.DbCommand command) => command.CommandText = {|TG002:"DELETE FROM Invoices"|};

                public static void PurgeThroughTheInterface(System.Data.IDbCommand command) => command.CommandText = {|TG002:"DELETE FROM Invoices"|};

                public static void PurgeMine(System.Data.Common.DbCommand command) => command.CommandText = "DELETE FROM Invoices WHERE TenantId = $tenant";

                public static Microsoft.Data.Sqlite.SqliteCommand Initialized() => new() { CommandText = {|TG002:"DELETE FROM Invoices"|} };
            }
            """);

    [Fact]
    public Task Command_constructors_are_checked()
        => Tg002.VerifyAsync("""
            public static class Jobs
            {
                public static Microsoft.Data.Sqlite.SqliteCommand Purge() => new({|TG002:"DELETE FROM Invoices"|});

                public static Microsoft.Data.Sqlite.SqliteCommand PurgeMine(Microsoft.Data.Sqlite.SqliteConnection connection)
                    => new Microsoft.Data.Sqlite.SqliteCommand("DELETE FROM Invoices WHERE TenantId = $tenant", connection);

                public static Microsoft.Data.Sqlite.SqliteCommand Empty() => new();
            }
            """);

    [Fact]
    public Task ADO_NET_SQL_built_at_runtime_is_TG004()
        => Tg004.VerifyAsync("""
            public static class Jobs
            {
                public static void Run(System.Data.Common.DbCommand command, string sql) => command.CommandText = {|TG004:sql|};

                public static Microsoft.Data.Sqlite.SqliteCommand Create(string sql) => new({|TG004:sql|});
            }
            """);

    [Fact]
    public Task Other_CommandText_properties_are_not_checked()
        => Tg002.VerifyAsync("""
            public sealed class Template
            {
                public string CommandText { get; set; } = "";
            }

            public static class Jobs
            {
                public static void Set(Template template) => template.CommandText = "DELETE FROM Invoices";
            }
            """);

    [Fact]
    public Task Attribute_clears_Dapper_and_ADO_NET()
        => Tg002.VerifyAsync("""
            namespace App
            {
                using System.Data;
                using Dapper;
                using TenantGuard;

                public static class Health
                {
                    [AllowCrossTenantAccess("a health check that reads no rows")]
                    public static object? Ping(IDbConnection connection) => connection.ExecuteScalar("SELECT 1");

                    [AllowCrossTenantAccess("a health check that reads no rows")]
                    public static void PingCommand(System.Data.Common.DbCommand command) => command.CommandText = "SELECT 1";
                }
            }
            """ + Dapper);

    [Fact]
    public Task Configured_methods_are_checked()
        => Tg002.VerifyAsync(
            """
            namespace Acme.Data
            {
                public static class Sql
                {
                    public static int Execute(System.Data.IDbConnection connection, string sql, params object[] parameters) => 0;

                    public static int Execute(System.Data.IDbConnection connection, int timeout, string sql) => 0;

                    public static T Query<T>(this System.Data.IDbConnection connection, System.FormattableString sql) => default!;

                    public static int Other(System.Data.IDbConnection connection, string sql) => 0;

                    public static class Nested
                    {
                        public static int Run(string label, string sql) => 0;
                    }
                }
            }

            namespace App
            {
                using Acme.Data;

                public static class Jobs
                {
                    public static int Purge(System.Data.IDbConnection connection) => Sql.Execute(connection, {|TG002:"DELETE FROM Invoices"|});

                    public static int PurgeWithTimeout(System.Data.IDbConnection connection) => Sql.Execute(connection, 30, {|TG002:"DELETE FROM Invoices"|});

                    public static Invoice One(System.Data.IDbConnection connection, int id) => connection.Query<Invoice>({|TG002:$"SELECT * FROM Invoices WHERE Id = {id}"|});

                    public static int NotListed(System.Data.IDbConnection connection) => Sql.Other(connection, "DELETE FROM Invoices");

                    public static int Nested(System.Data.IDbConnection connection) => Sql.Nested.Run({|TG002:"purge"|}, "DELETE FROM Invoices");
                }
            }
            """,
            "tenantguard.raw_sql_methods = Acme.Data.Sql.Execute, Acme.Data.Sql.Query, Acme.Data.Sql.Nested.Run # wrappers");

    [Fact]
    public Task Configured_methods_are_matched_with_case()
        => Tg002.VerifyAsync(
            """
            namespace Acme.Data
            {
                public static class Sql
                {
                    public static int Execute(string sql) => 0;
                }
            }

            public static class Jobs
            {
                public static int Purge() => Acme.Data.Sql.Execute("DELETE FROM Invoices");
            }
            """,
            "tenantguard.raw_sql_methods = acme.data.sql.execute");
}
