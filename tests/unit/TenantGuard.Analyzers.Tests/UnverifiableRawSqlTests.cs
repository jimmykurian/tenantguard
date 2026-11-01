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

        (rule.Id, rule.DefaultSeverity).Should().Be(("TG004", DiagnosticSeverity.Info));
        rule.MessageFormat.ToString(CultureInfo.InvariantCulture).Should().Be("Raw SQL built at runtime cannot be checked for a tenant predicate.");
    }
}
