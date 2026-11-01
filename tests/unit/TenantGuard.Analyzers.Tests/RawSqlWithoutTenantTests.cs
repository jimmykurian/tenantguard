// <copyright file="RawSqlWithoutTenantTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Verify = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.RawSqlWithoutTenantAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// Analyzer TG002 against code that should and shouldn't report it.
public sealed class RawSqlWithoutTenantTests
{
    [Fact]
    public Task Literal_without_the_tenant_column_is_reported()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteAll(AppDb db) => db.Database.ExecuteSqlRaw({|TG002:"DELETE FROM Invoices"|});
            }
            """);

    [Theory]
    [InlineData("\"DELETE FROM Invoices WHERE TenantId = {0}\"")]
    [InlineData("\"delete from invoices where tenantid = {0}\"")]
    [InlineData("\"\"\"\n        DELETE FROM Invoices\n        WHERE TenantId = {0}\n        \"\"\"")]
    public Task Literal_with_the_tenant_column_is_not_reported(string sql)
        => Verify.VerifyAsync($$"""
            public static class Jobs
            {
                public static int Delete(AppDb db, string tenant) => db.Database.ExecuteSqlRaw({{sql}}, tenant);
            }
            """);

    [Fact]
    public Task SQL_in_a_constant_is_read()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                private const string DeleteAllSql = "DELETE FROM Invoices";
                private const string DeleteMineSql = "DELETE FROM Invoices WHERE " + nameof(Invoice.TenantId) + " = {0}";

                public static int DeleteAll(AppDb db) => db.Database.ExecuteSqlRaw({|TG002:DeleteAllSql|});

                public static int DeleteMine(AppDb db, string tenant) => db.Database.ExecuteSqlRaw(DeleteMineSql, tenant);
            }
            """);

    [Fact]
    public Task Interpolated_SQL_is_read()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteOne(AppDb db, int id) => db.Database.ExecuteSql({|TG002:$"DELETE FROM Invoices WHERE Id = {id}"|});

                public static int DeleteMine(AppDb db, string tenant) => db.Database.ExecuteSql($"DELETE FROM Invoices WHERE TenantId = {tenant}");

                public static int DeleteNamed(AppDb db, string tenant) => db.Database.ExecuteSql($"DELETE FROM Invoices WHERE {nameof(Invoice.TenantId)} = {tenant}");
            }
            """);

    [Fact]
    public Task Interpolated_string_passed_as_raw_SQL_is_read()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteOne(AppDb db, int id) => db.Database.ExecuteSqlRaw({|TG002:$"DELETE FROM Invoices WHERE Id = {id}"|});
            }
            """);

    [Theory]
    [InlineData("db.Database.ExecuteSqlRaw({|TG002:\"DELETE FROM Invoices\"|})")]
    [InlineData("db.Database.ExecuteSqlRawAsync({|TG002:\"DELETE FROM Invoices\"|})")]
    [InlineData("db.Database.ExecuteSqlInterpolated({|TG002:$\"DELETE FROM Invoices WHERE Id = {id}\"|})")]
    [InlineData("db.Database.ExecuteSqlInterpolatedAsync({|TG002:$\"DELETE FROM Invoices WHERE Id = {id}\"|})")]
    [InlineData("db.Database.ExecuteSql({|TG002:$\"DELETE FROM Invoices WHERE Id = {id}\"|})")]
    [InlineData("db.Database.ExecuteSqlAsync({|TG002:$\"DELETE FROM Invoices WHERE Id = {id}\"|})")]
    [InlineData("db.Database.SqlQueryRaw<int>({|TG002:\"SELECT Id FROM Invoices\"|})")]
    [InlineData("db.Database.SqlQuery<int>({|TG002:$\"SELECT Id FROM Invoices WHERE Id > {id}\"|})")]
    public Task Every_raw_SQL_method_is_checked(string call)
        => Verify.VerifyAsync($$"""
            public static class Jobs
            {
                public static object Run(AppDb db, int id) => {{call}};
            }
            """);

    [Fact]
    public Task Tenant_column_comes_from_editorconfig()
    {
        var test = Verify.Create(
            """
            public static class Jobs
            {
                public static int DeleteMine(AppDb db, string org) => db.Database.ExecuteSqlRaw("DELETE FROM Invoices WHERE OrgId = {0}", org);

                public static int DeleteAll(AppDb db, string tenant) => db.Database.ExecuteSqlRaw({|#0:"DELETE FROM Invoices WHERE TenantId = {0}"|}, tenant);
            }
            """,
            "tenantguard.tenant_column = OrgId");
        test.ExpectedDiagnostics.Add(new DiagnosticResult(RawSqlWithoutTenantAnalyzer.Rule).WithLocation(0).WithMessage(
            "Raw SQL bypasses query filters and does not mention 'OrgId'. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")]."));

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task Every_tenant_column_counts()
    {
        var test = Verify.Create(
            """
            public static class Jobs
            {
                public static int DeleteMine(AppDb db, string org) => db.Database.ExecuteSqlRaw("DELETE FROM Invoices WHERE OrgId = {0}", org);

                public static int DeleteAll(AppDb db) => db.Database.ExecuteSqlRaw({|#0:"DELETE FROM Invoices"|});
            }
            """,
            "tenantguard.tenant_column = TenantId, OrgId");
        test.ExpectedDiagnostics.Add(new DiagnosticResult(RawSqlWithoutTenantAnalyzer.Rule).WithLocation(0).WithMessage(
            "Raw SQL bypasses query filters and does not mention 'TenantId' or 'OrgId'. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")]."));

        return test.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task Comment_after_an_editorconfig_value_is_not_part_of_it()
        => Verify.VerifyAsync(
            """
            public static class Jobs
            {
                public static int DeleteMine(AppDb db, string org) => db.Database.ExecuteSqlRaw("DELETE FROM Invoices WHERE OrgId = {0}", org);
            }
            """,
            "tenantguard.tenant_column = OrgId           # the tenant column");

    [Fact]
    public Task Attribute_clears_it()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                [AllowCrossTenantAccess("purges every tenant's test data")]
                public static int DeleteAll(AppDb db) => db.Database.ExecuteSqlRaw("DELETE FROM Invoices");
            }
            """);

    [Fact]
    public Task SQL_built_at_runtime_is_left_to_TG004()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int Run(AppDb db, string sql) => db.Database.ExecuteSqlRaw(sql);
            }
            """);

    [Fact]
    public Task FromSql_is_not_reported()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static int CountRaw(AppDb db) => db.Invoices.FromSqlRaw("SELECT * FROM Invoices").Count();

                public static int CountOne(AppDb db, int id) => db.Invoices.FromSql($"SELECT * FROM Invoices WHERE Id = {id}").Count();
            }
            """);

    [Fact]
    public void Rule_matches_the_spec()
    {
        var rule = RawSqlWithoutTenantAnalyzer.Rule;

        (rule.Id, rule.DefaultSeverity).Should().Be(("TG002", DiagnosticSeverity.Warning));
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be("Raw SQL bypasses query filters and does not mention '{0}'. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")].");
    }
}
