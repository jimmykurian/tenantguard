// <copyright file="RuleResults.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Architecture.Tests.Support;

internal static class RuleResults
{
    // Fails with each type that breaks the rule and NetArchTest's explanation of why.
    public static void ShouldPass(this NetArchTest.Rules.TestResult result)
    {
        result.SelectedTypesForTesting.Should().NotBeEmpty("a rule that selects no types checks nothing");
        var failures = (result.FailingTypes ?? []).Select(type => $"{type.FullName}: {type.Explanation}");
        failures.Should().BeEmpty();
    }
}
