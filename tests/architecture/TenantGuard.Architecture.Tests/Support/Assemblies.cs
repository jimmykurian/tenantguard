// <copyright file="Assemblies.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using TenantGuard.Testing;

namespace TenantGuard.Architecture.Tests.Support;

// The shipped assemblies the rules check.
internal static class Assemblies
{
    public static Assembly Runtime => typeof(TenantGuardModel).Assembly;

    public static Assembly TestKit => typeof(TenantIsolationProbeException).Assembly;

    // Loaded from the output folder: the analyzer project keeps Roslyn private, so its types
    // can't be named here.
    public static string AnalyzersPath => Path.Combine(AppContext.BaseDirectory, "TenantGuard.Analyzers.dll");

    // The tool and its payload are programs that share source files, so they're loaded from the
    // output folder rather than named.
    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "TenantGuard.Tool.dll");

    public static string PayloadPath => Path.Combine(AppContext.BaseDirectory, "TenantGuard.Tool.Payload.dll");

    // The test adapters, read from their own output folders.
    public static string XunitV3AdapterPath => Metadata("XunitV3Adapter");

    public static string TUnitAdapterPath => Metadata("TUnitAdapter");

    private static string Metadata(string key)
        => Path.GetFullPath(typeof(Assemblies).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!);
}
