// <copyright file="RuleCatalog.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// The model rules' IDs by severity, as docs/reference/rules.md lists them, for Suppress to check.
internal static class RuleCatalog
{
    public const string UnguardedEntity = "TGM001";

    public const string UnscopedEntity = "TGM002";

    public static readonly IReadOnlySet<string> Errors = new HashSet<string>(StringComparer.Ordinal)
    {
        "TGM001", "TGM002", "TGM003", "TGM004", "TGM006", "TGM010",
    };

    public static readonly IReadOnlySet<string> Warnings = new HashSet<string>(StringComparer.Ordinal)
    {
        "TGM005", "TGM007", "TGM008", "TGM009",
    };
}
