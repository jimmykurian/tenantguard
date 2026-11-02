// <copyright file="DependencyRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using NetArchTest.Rules;
using TenantGuard.Architecture.Tests.Support;

namespace TenantGuard.Architecture.Tests;

// What each shipped assembly may depend on (AGENTS.md, Decisions already made).
public sealed class DependencyRules
{
    // Test frameworks and assertion libraries, which no shipped assembly may use.
    private static readonly string[] _testLibraries =
        ["Xunit", "NUnit", "Microsoft.VisualStudio.TestTools", "TUnit", "AwesomeAssertions", "FluentAssertions"];

    [Fact]
    public void Runtime_depends_on_EF_Core_and_what_it_brings_only()
        => Types.InAssembly(Assemblies.Runtime)
            .Should().OnlyHaveDependencyOn("System", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "TenantGuard")
            .GetResult().ShouldPass();

    [Fact]
    public void Runtime_does_not_depend_on_the_test_kit()
        => Types.InAssembly(Assemblies.Runtime)
            .ShouldNot().HaveDependencyOnAny("TenantGuard.Testing")
            .GetResult().ShouldPass();

    [Fact]
    public void Test_kit_depends_on_EF_Core_and_the_runtime_only()
        => Types.InAssembly(Assemblies.TestKit)
            .Should().OnlyHaveDependencyOn("System", "Microsoft.EntityFrameworkCore", "Microsoft.Extensions", "TenantGuard")
            .GetResult().ShouldPass();

    [Fact]
    public void Test_kit_works_with_any_test_framework()
        => Types.InAssembly(Assemblies.TestKit)
            .ShouldNot().HaveDependencyOnAny(_testLibraries)
            .GetResult().ShouldPass();

    [Fact]
    public void Analyzers_depend_on_Roslyn_only()
        => Types.FromFile(Assemblies.AnalyzersPath)
            .Should().OnlyHaveDependencyOn("System", "Microsoft.CodeAnalysis", "TenantGuard.Analyzers")
            .GetResult().ShouldPass();
}
