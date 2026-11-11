// <copyright file="UnverifiableRawSqlTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.CodeAnalysis;
using Verify = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.UnverifiableRawSqlAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// Analyzer TG004 against code that should and shouldn't report it.
public sealed class UnverifiableRawSqlTests
{
    [Fact]
    public Task SQL_from_a_variable_is_reported()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int Run(AppDb db, string sql) => db.Database.ExecuteSqlRaw({|TG004:sql|});
            }
            """);

    [Fact]
    public Task Concatenated_SQL_is_reported()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteMine(AppDb db, string tenant) => db.Database.ExecuteSqlRaw({|TG004:"DELETE FROM Invoices WHERE TenantId = '" + tenant + "'"|});
            }
            """);

    [Theory]
    [InlineData("db.Database.ExecuteSqlRawAsync({|TG004:sql|})")]
    [InlineData("db.Database.ExecuteSqlAsync({|TG004:query|})")]
    [InlineData("db.Database.ExecuteSqlInterpolatedAsync({|TG004:query|})")]
    [InlineData("db.Database.SqlQuery<int>({|TG004:query|})")]
    public Task Every_raw_SQL_method_is_checked(string call)
        => Verify.VerifyAsync($$"""
            public static class Jobs
            {
                public static object Run(AppDb db, string sql, FormattableString query) => {{call}};
            }
            """);

    [Fact]
    public Task Literal_and_interpolated_SQL_are_left_to_TG002()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteAll(AppDb db) => db.Database.ExecuteSqlRaw("DELETE FROM Invoices");

                public static int DeleteOne(AppDb db, int id) => db.Database.ExecuteSql($"DELETE FROM Invoices WHERE Id = {id}");
            }
            """);

    [Fact]
    public Task An_interpolated_hole_with_a_format_or_an_alignment_is_built_at_runtime()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int Formatted(AppDb db) => db.Database.ExecuteSqlRaw({|TG004:$"DELETE FROM Invoices WHERE TenantId = {42:'TenantId'}"|});

                public static int Aligned(AppDb db, string tenant) => db.Database.ExecuteSqlRaw({|TG004:$"DELETE FROM Invoices WHERE TenantId = {tenant,10}"|});
            }
            """);

    [Fact]
    public Task A_local_written_again_is_built_at_runtime()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int Reassigned(AppDb db, bool paid)
                {
                    var sql = "DELETE FROM Invoices WHERE TenantId = {0}";
                    if (paid)
                    {
                        sql = "DELETE FROM Invoices";
                    }

                    return db.Database.ExecuteSqlRaw({|TG004:sql|}, "acme");
                }

                public static int Appended(AppDb db)
                {
                    var sql = "DELETE FROM Invoices WHERE TenantId = {0}";
                    sql += " OR 1 = 1";
                    return db.Database.ExecuteSqlRaw({|TG004:sql|}, "acme");
                }

                public static int ByRef(AppDb db)
                {
                    var sql = "DELETE FROM Invoices WHERE TenantId = {0}";
                    Widen(ref sql);
                    return db.Database.ExecuteSqlRaw({|TG004:sql|}, "acme");
                }

                public static int InALambda(AppDb db)
                {
                    var sql = "DELETE FROM Invoices WHERE TenantId = {0}";
                    Action widen = () => sql = "DELETE FROM Invoices";
                    widen();
                    return db.Database.ExecuteSqlRaw({|TG004:sql|}, "acme");
                }

                public static int FromAParameter(AppDb db, string text)
                {
                    var sql = text;
                    return db.Database.ExecuteSqlRaw({|TG004:sql|});
                }

                private static void Widen(ref string sql) => sql = "DELETE FROM Invoices";
            }
            """);

    [Fact]
    public Task A_local_set_once_to_readable_SQL_is_left_to_TG002()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int DeleteMine(AppDb db, string tenant)
                {
                    var sql = "DELETE FROM Invoices WHERE TenantId = {0}";
                    return db.Database.ExecuteSqlRaw(sql, tenant);
                }
            }
            """);

    [Fact]
    public Task FromSql_built_at_runtime_on_a_type_without_a_tenant_member_is_reported_under_tenant_members_only()
        => Verify.VerifyAsync(
            """
            public sealed class Country
            {
                public int Id { get; set; }
            }

            public sealed class Lookups : DbContext
            {
                public DbSet<Country> Countries => Set<Country>();
            }

            public static class Reports
            {
                public static int Countries(Lookups db, string sql) => db.Countries.FromSqlRaw({|TG004:sql|}).Count();
            }
            """,
            "tenantguard.tenant_members_only = true");

    [Fact]
    public Task Attribute_clears_it()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                [AllowCrossTenantAccess("runs migration scripts for every tenant")]
                public static int Run(AppDb db, string sql) => db.Database.ExecuteSqlRaw(sql);
            }
            """);

    [Fact]
    public void Rule_matches_the_spec()
    {
        var rule = UnverifiableRawSqlAnalyzer.Rule;

        (rule.Id, rule.DefaultSeverity).Should().Be(("TG004", DiagnosticSeverity.Warning));
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be("Raw SQL built at runtime cannot be checked for a tenant predicate.");
    }
}
