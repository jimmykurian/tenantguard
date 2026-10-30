// <copyright file="SmokeTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace TenantGuard.Analyzers.Tests;

public sealed class SmokeTests
{
    [Fact]
    public async Task Verifier_compiles_a_source_with_no_diagnostics()
    {
        var test = new CSharpAnalyzerTest<EmptyDiagnosticAnalyzer, DefaultVerifier>
        {
            TestCode = "public sealed class Invoice { public string TenantId { get; set; } = \"acme\"; }",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}
