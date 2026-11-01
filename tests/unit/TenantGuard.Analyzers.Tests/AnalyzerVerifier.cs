// <copyright file="AnalyzerVerifier.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Analyzers.Tests;

// Runs one analyzer over app code that sees EF Core, its relational extensions and the TenantGuard
// runtime, the way a project with the package does. Diagnostics come from {|TG00n:...|} markup.
internal static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    // An app's entity and context for the code under test, which follows it in the same file.
    private const string App = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using System.Threading.Tasks;
        using Microsoft.EntityFrameworkCore;
        using TenantGuard;

        public sealed class Invoice
        {
            public int Id { get; set; }

            public string TenantId { get; set; } = "";
        }

        public sealed class AppDb : DbContext
        {
            public DbSet<Invoice> Invoices => Set<Invoice>();
        }

        """;

    public static Task VerifyAsync(string code, string? editorConfig = null)
        => Create(code, editorConfig).RunAsync(TestContext.Current.CancellationToken);

    // withRuntime: false leaves out TenantGuard.EntityFrameworkCore, for code that declares the attribute itself.
    public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> Create(string code, string? editorConfig = null, bool withRuntime = true)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = App + code,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(DbContext).Assembly.Location));
        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(RelationalDatabaseFacadeExtensions).Assembly.Location));
        if (withRuntime)
        {
            test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(AllowCrossTenantAccessAttribute).Assembly.Location));
        }

        if (editorConfig is not null)
        {
            test.TestState.AnalyzerConfigFiles.Add(("/.editorconfig", $"root = true\n\n[*]\n{editorConfig}\n"));
        }

        return test;
    }
}
