// <copyright file="IntegrationScope.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.XUnit3;
using Xunit.v3;

[assembly: TestPipelineStartup(typeof(TenantGuard.IntegrationTests.IntegrationScope))]

namespace TenantGuard.IntegrationTests;

// Starts LightBDD for an integration test project; every one links this file in through
// tests/integration/Directory.Build.props. The report lands in the test output's Reports/.
//
// Scenarios run from [Fact], not LightBDD's [Scenario]: LightBDD.XUnit3 3.12.1 does not load
// on xunit.v3 4, and the test platform then drops every [Scenario] test without a word
// (LightBDD#409). task lint:scenarios fails on [Scenario] until that is fixed.
internal sealed class IntegrationScope : LightBddScope;
