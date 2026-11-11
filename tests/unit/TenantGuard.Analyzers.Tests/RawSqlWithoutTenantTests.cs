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
            "Raw SQL bypasses query filters and does not compare 'OrgId' with a value. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")]."));

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
            "Raw SQL bypasses query filters and does not compare 'TenantId' or 'OrgId' with a value. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")]."));

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

    [Theory]
    [InlineData("SELECT * FROM Invoices WHERE OwningTenantId = @tenant")]
    [InlineData("SELECT * FROM Invoices WHERE Id = @TenantId")]
    [InlineData("SELECT TenantId, COUNT(*) FROM Invoices GROUP BY TenantId")]
    [InlineData("SELECT l.* FROM Lines l JOIN Invoices i ON i.TenantId = l.TenantId")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId <> @tenant")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId != @tenant")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId >= 1")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId IS NULL")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId IN (SELECT TenantId FROM Tenants)")]
    [InlineData("SELECT * FROM Invoices WHERE Note = 'TenantId = 1'")]
    [InlineData("SELECT * FROM Invoices -- WHERE TenantId = @tenant")]
    [InlineData("SELECT * FROM Invoices /* WHERE TenantId = @tenant */")]
    [InlineData("SELECT * FROM Invoices /* outer /* inner */ TenantId = @tenant */")]
    [InlineData("SELECT * FROM Invoices WHERE @tenant = TenantId()")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId() = @tenant")]
    [InlineData("SELECT TenantId FROM Invoices WHERE 'acme' = TenantId$backup")]
    [InlineData("SELECT TenantId FROM Invoices WHERE TenantId#old = @tenant")]
    public Task SQL_that_names_the_column_without_comparing_it_with_a_value_is_reported(string sql)
        => Verify.VerifyAsync($$"""
            public static class Jobs
            {
                public static int Run(AppDb db) => db.Database.ExecuteSqlRaw({|TG002:"{{sql}}"|});
            }
            """);

    [Theory]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = @tenant")]
    [InlineData("SELECT * FROM Invoices WHERE \"TenantId\" = :tenant")]
    [InlineData("SELECT * FROM Invoices WHERE [TenantId] = $tenant")]
    [InlineData("SELECT * FROM Invoices WHERE `TenantId` = ?")]
    [InlineData("SELECT * FROM Invoices i WHERE i.TenantId = $1")]
    [InlineData("SELECT * FROM [dbo].[Invoices] WHERE [dbo].[Invoices].[TenantId] = {0}")]
    [InlineData("SELECT * FROM \"Invoices\" i WHERE \"i\".\"tenantid\" = @tenant")]
    [InlineData("SELECT * FROM Invoices i WHERE @tenant = i.TenantId")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId IN @tenants")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId IN (@acme, @globex)")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = ANY (@tenants)")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = 'acme'")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = N'acme'")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId=42")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = -1")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId IN (-1, +2)")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId /* the scope */ = @tenant")]
    [InlineData("SELECT * FROM Invoices WHERE TenantId = @tenant -- the scope")]
    [InlineData("INSERT INTO Invoices (Number, TenantId) VALUES (@number, @tenant)")]
    [InlineData("INSERT INTO [dbo].[Invoices] ([TenantId], [Number]) SELECT @tenant, @number")]
    public Task SQL_that_compares_the_column_with_a_value_is_not_reported(string sql)
        => Verify.VerifyAsync($$""""
            public static class Jobs
            {
                public static int Run(AppDb db) => db.Database.ExecuteSqlRaw("""{{sql}}""");
            }
            """");

    [Fact]
    public Task An_interpolated_hole_is_a_value_and_a_constant_hole_is_text()
        => Verify.VerifyAsync("""
            public static class Jobs
            {
                public static int Mine(AppDb db, string tenant) => db.Database.ExecuteSql($"DELETE FROM Invoices WHERE {nameof(Invoice.TenantId)} = {tenant}");

                public static int Join(AppDb db) => db.Database.ExecuteSql({|TG002:$"DELETE FROM Invoices WHERE TenantId = {nameof(Invoice.TenantId)}"|});

                public static int Spelled(AppDb db) => db.Database.ExecuteSqlRaw({|TG002:$"DELETE FROM Invoices WHERE TenantId = {'T'}enantId"|});

                public static int Numbered(AppDb db) => db.Database.ExecuteSqlRaw($"DELETE FROM Invoices WHERE TenantId = {42}");
            }
            """);

    [Fact]
    public Task SQL_in_a_local_set_once_is_read()
        => Verify.VerifyAsync(""""
            public static class Jobs
            {
                public static int DeleteAll(AppDb db)
                {
                    var sql = """
                        DELETE FROM Invoices
                        """;
                    return db.Database.ExecuteSqlRaw({|TG002:sql|});
                }

                public static int DeleteMine(AppDb db, string tenant)
                {
                    var sql = "DELETE FROM Invoices " + "WHERE TenantId = {0}";
                    return db.Database.ExecuteSqlRaw(sql, tenant);
                }

                public static int DeleteForgiven(AppDb db)
                {
                    string? sql = "DELETE FROM Invoices";
                    return db.Database.ExecuteSqlRaw({|TG002:sql|}!);
                }

                public static int DeleteOne(AppDb db, int id)
                {
                    FormattableString sql = $"DELETE FROM Invoices WHERE Id = {id}";
                    var again = sql;
                    return db.Database.ExecuteSql({|TG002:again|});
                }
            }
            """");

    [Fact]
    public Task FromSql_on_a_type_without_a_tenant_member_is_checked_under_tenant_members_only()
        => Verify.VerifyAsync(
            """
            public sealed class Country
            {
                public int Id { get; set; }
            }

            public sealed class Lookups : DbContext
            {
                public DbSet<Country> Countries => Set<Country>();

                public DbSet<Invoice> Invoices => Set<Invoice>();
            }

            public static class Reports
            {
                public static int Countries(Lookups db) => db.Countries.FromSqlRaw({|TG002:"SELECT * FROM Countries"|}).Count();

                public static int Invoices(Lookups db) => db.Invoices.FromSqlRaw("SELECT * FROM Invoices").Count();
            }
            """,
            "tenantguard.tenant_members_only = true");

    [Fact]
    public Task FromSql_listed_in_raw_sql_methods_is_checked()
        => Verify.VerifyAsync(
            """
            public static class Reports
            {
                public static int Count(AppDb db) => db.Invoices.FromSqlRaw({|TG002:"SELECT * FROM Invoices"|}).Count();
            }
            """,
            "tenantguard.raw_sql_methods = Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlRaw");

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
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be("Raw SQL bypasses query filters and does not compare '{0}' with a value. Add a tenant predicate, or [AllowCrossTenantAccess(\"why\")].");
    }
}
