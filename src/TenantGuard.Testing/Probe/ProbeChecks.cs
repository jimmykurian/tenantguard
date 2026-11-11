// <copyright file="ProbeChecks.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

// The probe's six checks, by the names ProbeRow.Check carries, NavigationLeak, one per navigation, and
// NoTenantIsolation, with WithoutTenant.
internal static class ProbeChecks
{
    public const string ReadIsolation = "ReadIsolation";

    public const string FindIsolation = "FindIsolation";

    public const string BulkIsolation = "BulkIsolation";

    public const string UpdateGuard = "UpdateGuard";

    public const string InsertGuard = "InsertGuard";

    public const string DeleteGuard = "DeleteGuard";

    public const string NavigationLeak = "NavigationLeak";

    public const string NoTenantIsolation = "NoTenantIsolation";

    // The checks that only read, which run for every entity type before any check writes.
    public static readonly IReadOnlyList<string> Reads = [ReadIsolation, FindIsolation];

    public static readonly IReadOnlyList<string> Writes = [BulkIsolation, UpdateGuard, InsertGuard, DeleteGuard];

    public static readonly IReadOnlyList<string> All = [.. Reads, .. Writes];

    // Such as NavigationLeak(Lines).
    public static string NavigationLeakOf(string navigation) => $"{NavigationLeak}({navigation})";
}
