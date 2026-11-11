// <copyright file="ValidateToolSwitch.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// Set by the validate tool's payload while it runs: the tool prints the model check itself, so the
// convention doesn't log it too (ADR 0033). Internal: only the tool sets it.
internal static class ValidateToolSwitch
{
    public const string Name = "TenantGuard.ValidateToolRunning";

    public static bool IsOn => AppContext.TryGetSwitch(Name, out var on) && on;
}
