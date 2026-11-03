// <copyright file="TestResult.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ProbeAdapters.Tests;

// One test a fixture ran: its method, its name, Passed, Failed or Skipped, and the failure message
// or skip reason.
internal sealed record TestResult(string Method, string Name, string Outcome, string Message);
