// <copyright file="IgnoreQueryFiltersTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.CodeAnalysis;
using Verify = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.IgnoreQueryFiltersAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// TG001 (SPEC 6).
public sealed class IgnoreQueryFiltersTests
{
    private const string TenantFilterNames = "tenantguard.tenant_filter_names = TenantFilter";

    [Fact]
    public Task IgnoreQueryFilters_is_reported_at_the_call()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static int CountAll(AppDb db) => db.Invoices.Where(invoice => invoice.Id > 0).{|TG001:IgnoreQueryFilters()|}.Count();
            }
            """);

    [Fact]
    public Task Static_call_is_reported()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static int CountAll(AppDb db) => EntityFrameworkQueryableExtensions.{|TG001:IgnoreQueryFilters(db.Invoices)|}.Count();
            }
            """);

    [Fact]
    public Task Attribute_on_the_method_clears_it()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                [AllowCrossTenantAccess("billing report across tenants")]
                public static int CountAll(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
            }
            """);

    [Fact]
    public Task Attribute_on_the_class_clears_it()
        => Verify.VerifyAsync("""
            [AllowCrossTenantAccess("billing reports across tenants")]
            public static class Reports
            {
                public static int CountAll(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
            }
            """);

    [Fact]
    public Task Attribute_on_an_outer_class_clears_it()
        => Verify.VerifyAsync("""
            [AllowCrossTenantAccess("admin tools work across tenants")]
            public static class Admin
            {
                public static class Reports
                {
                    public static int CountAll(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
                }
            }
            """);

    [Fact]
    public Task Attribute_on_a_local_function_clears_it()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static int CountAll(AppDb db)
                {
                    return Count(db);

                    [AllowCrossTenantAccess("billing report across tenants")]
                    static int Count(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
                }
            }
            """);

    [Fact]
    public Task Attribute_on_a_lambda_clears_it()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static readonly Func<AppDb, int> CountAll =
                    [AllowCrossTenantAccess("billing report across tenants")] (AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
            }
            """);

    [Fact]
    public Task Attribute_on_another_method_does_not_clear_it()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                [AllowCrossTenantAccess("billing report across tenants")]
                public static int CountAll(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();

                public static int CountMine(AppDb db) => db.Invoices.{|TG001:IgnoreQueryFilters()|}.Count();
            }
            """);

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public Task Attribute_without_a_reason_does_not_clear_it(string reason)
        => Verify.VerifyAsync($$"""
            public static class Reports
            {
                [AllowCrossTenantAccess({{reason}})]
                public static int CountAll(AppDb db) => db.Invoices.{|TG001:IgnoreQueryFilters()|}.Count();
            }
            """);

    [Fact]
    public Task Attribute_declared_in_the_project_clears_it()
        => Verify.Create(
            """
            namespace TenantGuard
            {
                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
                public sealed class AllowCrossTenantAccessAttribute(string reason) : Attribute
                {
                    public string Reason { get; } = reason;
                }
            }

            public static class Reports
            {
                [AllowCrossTenantAccess("billing report across tenants")]
                public static int CountAll(AppDb db) => db.Invoices.IgnoreQueryFilters().Count();
            }
            """,
            withRuntime: false).RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Named_overload_is_reported_when_no_tenant_filter_names_are_set()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static int CountDeleted(AppDb db) => db.Invoices.{|TG001:IgnoreQueryFilters(["SoftDelete"])|}.Count();
            }
            """);

    [Theory]
    [InlineData("[\"SoftDelete\"]")]
    [InlineData("new[] { \"SoftDelete\" }")]
    [InlineData("new string[] { \"SoftDelete\", \"Archived\" }")]
    [InlineData("new List<string> { \"SoftDelete\" }")]
    [InlineData("new HashSet<string> { \"SoftDelete\" }")]
    public Task Constant_names_that_leave_the_tenant_filter_on_are_not_reported(string names)
        => Verify.VerifyAsync(
            $$"""
            public static class Reports
            {
                public static int CountDeleted(AppDb db) => db.Invoices.IgnoreQueryFilters({{names}}).Count();
            }
            """,
            TenantFilterNames);

    [Fact]
    public Task Names_that_include_a_tenant_filter_are_reported()
        => Verify.VerifyAsync(
            """
            public static class Reports
            {
                public static int CountAll(AppDb db) => db.Invoices.{|TG001:IgnoreQueryFilters(["SoftDelete", "TenantFilter"])|}.Count();
            }
            """,
            TenantFilterNames);

    [Fact]
    public Task Names_from_a_variable_are_reported()
        => Verify.VerifyAsync(
            """
            public static class Reports
            {
                public static int Count(AppDb db, IReadOnlyCollection<string> names) => db.Invoices.{|TG001:IgnoreQueryFilters(names)|}.Count();
            }
            """,
            TenantFilterNames);

    [Fact]
    public Task Names_with_a_spread_are_reported()
        => Verify.VerifyAsync(
            """
            public static class Reports
            {
                public static int Count(AppDb db, string[] more) => db.Invoices.{|TG001:IgnoreQueryFilters(["SoftDelete", .. more])|}.Count();
            }
            """,
            TenantFilterNames);

    [Fact]
    public Task Every_tenant_filter_name_counts()
        => Verify.VerifyAsync(
            """
            public static class Reports
            {
                public static int CountDeleted(AppDb db) => db.Invoices.IgnoreQueryFilters(["SoftDelete"]).Count();

                public static int CountAll(AppDb db) => db.Invoices.{|TG001:IgnoreQueryFilters(["OrgFilter"])|}.Count();
            }
            """,
            "tenantguard.tenant_filter_names = TenantFilter, OrgFilter");

    [Fact]
    public Task Other_IgnoreQueryFilters_methods_are_not_reported()
        => Verify.VerifyAsync("""
            public static class Sequences
            {
                public static IEnumerable<T> IgnoreQueryFilters<T>(this IEnumerable<T> source) => source;
            }

            public static class Reports
            {
                public static int Count(List<Invoice> invoices) => invoices.IgnoreQueryFilters().Count();
            }
            """);

    [Fact]
    public void Rule_matches_the_spec()
    {
        var rule = IgnoreQueryFiltersAnalyzer.Rule;

        Assert.Equal(("TG001", DiagnosticSeverity.Warning), (rule.Id, rule.DefaultSeverity));
        Assert.Equal(
            "IgnoreQueryFilters disables the tenant filter. Add [AllowCrossTenantAccess(\"why\")] to the method, or ignore only the non-tenant filters by name.",
            rule.MessageFormat.ToString(CultureInfo.InvariantCulture));
    }
}
