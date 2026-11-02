// <copyright file="PublicSurfaceRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using NetArchTest.Rules;
using TenantGuard.Architecture.Tests.Support;
using TenantGuard.Testing;

namespace TenantGuard.Architecture.Tests;

// The shape of the public API: SPEC 2 fixes the namespaces, and the folders only group files.
public sealed class PublicSurfaceRules
{
    [Fact]
    public void Runtime_public_types_live_in_the_TenantGuard_namespace()
        => Types.InAssembly(Assemblies.Runtime)
            .That().ArePublic()
            .Should().ResideInNamespaceMatching("^TenantGuard$")
            .GetResult().ShouldPass();

    [Fact]
    public void Test_kit_public_types_live_in_the_TenantGuard_Testing_namespace()
        => Types.InAssembly(Assemblies.TestKit)
            .That().ArePublic()
            .Should().ResideInNamespaceMatching("^TenantGuard\\.Testing$")
            .GetResult().ShouldPass();

    [Fact]
    public void Exceptions_are_public_sealed_and_named_for_what_they_are()
        => Types.InAssemblies([Assemblies.Runtime, Assemblies.TestKit])
            .That().Inherit<Exception>()
            .Should().BePublic().And().BeSealed().And().HaveNameEndingWith("Exception")
            .GetResult().ShouldPass();

    [Fact]
    public void Analyzer_types_are_internal_except_the_analyzers_themselves()
        => Types.FromFile(Assemblies.AnalyzersPath)
            .That().DoNotHaveNameEndingWith("Analyzer")
            .Should().NotBePublic()
            .GetResult().ShouldPass();

    [Fact]
    public void Analyzers_are_sealed()
        => Types.FromFile(Assemblies.AnalyzersPath)
            .That().HaveNameEndingWith("Analyzer")
            .Should().BeSealed()
            .GetResult().ShouldPass();

    // Copy-on-write inside the assembly (TenantGuardOptions.With...), so the rule is that user
    // code can't change them once built.
    [Fact]
    public void Reports_results_and_options_cannot_be_changed_from_outside()
        => Types.InAssemblies([Assemblies.Runtime, Assemblies.TestKit])
            .That().AreOfType(typeof(TenantGuardOptions), typeof(ModelReport), typeof(ModelFinding), typeof(TenantViolation), typeof(ProbeReport), typeof(ProbeRow))
            .Should().BeImmutableExternally()
            .GetResult().ShouldPass();
}
