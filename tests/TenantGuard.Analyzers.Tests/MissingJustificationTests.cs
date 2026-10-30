// <copyright file="MissingJustificationTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.CodeAnalysis;
using Verify = TenantGuard.Analyzers.Tests.AnalyzerVerifier<TenantGuard.Analyzers.MissingJustificationAnalyzer>;

namespace TenantGuard.Analyzers.Tests;

// TG003 (SPEC 6).
public sealed class MissingJustificationTests
{
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    [InlineData("reason: \"\"")]
    public Task Attribute_on_a_method_without_a_reason_is_reported(string reason)
        => Verify.VerifyAsync($$"""
            public static class Reports
            {
                [{|TG003:AllowCrossTenantAccess({{reason}})|}]
                public static int CountAll(AppDb db) => db.Invoices.Count();
            }
            """);

    [Fact]
    public Task Attribute_on_a_class_without_a_reason_is_reported()
        => Verify.VerifyAsync("""
            [{|TG003:AllowCrossTenantAccess("")|}]
            public static class Reports
            {
            }
            """);

    [Fact]
    public Task Attribute_on_a_local_function_or_lambda_without_a_reason_is_reported()
        => Verify.VerifyAsync("""
            public static class Reports
            {
                public static readonly Func<int> Zero = [{|TG003:AllowCrossTenantAccess("")|}] () => 0;

                public static int One()
                {
                    return Count();

                    [{|TG003:AllowCrossTenantAccess(" ")|}]
                    static int Count() => 1;
                }
            }
            """);

    [Theory]
    [InlineData("AllowCrossTenantAccessAttribute")]
    [InlineData("TenantGuard.AllowCrossTenantAccess")]
    [InlineData("global::TenantGuard.AllowCrossTenantAccessAttribute")]
    public Task Every_spelling_of_the_name_is_read(string name)
        => Verify.VerifyAsync($$"""
            [{|TG003:{{name}}("")|}]
            public static class Reports
            {
            }
            """);

    [Fact]
    public Task Attribute_with_a_reason_is_not_reported()
        => Verify.VerifyAsync("""
            [AllowCrossTenantAccess("admin tools work across tenants")]
            public static class Reports
            {
                [AllowCrossTenantAccess("billing report across tenants")]
                public static int CountAll(AppDb db) => db.Invoices.Count();
            }
            """);

    [Fact]
    public Task Attribute_declared_in_the_project_without_a_reason_is_reported()
        => Verify.Create(
            """
            namespace TenantGuard
            {
                [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
                public sealed class AllowCrossTenantAccessAttribute : Attribute
                {
                    public string Reason { get; set; } = "";
                }
            }

            public static class Reports
            {
                [{|TG003:AllowCrossTenantAccess|}]
                public static int CountAll(AppDb db) => db.Invoices.Count();

                [{|TG003:AllowCrossTenantAccess(Reason = "")|}]
                public static int CountSome(AppDb db) => db.Invoices.Count();

                [AllowCrossTenantAccess(Reason = "billing report across tenants")]
                public static int CountMore(AppDb db) => db.Invoices.Count();
            }
            """,
            withRuntime: false).RunAsync(TestContext.Current.CancellationToken);

    [Fact]
    public Task Same_name_in_another_namespace_is_not_reported()
        => Verify.VerifyAsync("""
            namespace Other
            {
                public sealed class AllowCrossTenantAccessAttribute(string reason) : Attribute
                {
                    public string Reason { get; } = reason;
                }

                [AllowCrossTenantAccess("")]
                public static class Reports
                {
                }
            }
            """);

    [Fact]
    public void Rule_matches_the_spec()
    {
        var rule = MissingJustificationAnalyzer.Rule;

        Assert.Equal(("TG003", DiagnosticSeverity.Error), (rule.Id, rule.DefaultSeverity));
        Assert.Equal("AllowCrossTenantAccess needs a reason.", rule.MessageFormat.ToString(CultureInfo.InvariantCulture));
    }
}
