// <copyright file="Assemblies.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using TenantGuard.Testing;

namespace TenantGuard.Architecture.Tests.Support;

// The three shipped assemblies the rules check.
internal static class Assemblies
{
    public static Assembly Runtime => typeof(TenantGuardModel).Assembly;

    public static Assembly TestKit => typeof(TenantIsolationProbeException).Assembly;

    // Loaded from the output folder: the analyzer project keeps Roslyn private, so its types
    // can't be named here.
    public static string AnalyzersPath => Path.Combine(AppContext.BaseDirectory, "TenantGuard.Analyzers.dll");
}
