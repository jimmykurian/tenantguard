// <copyright file="TenantGuardLog.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Extensions.Logging;

namespace TenantGuard;

// SPEC 11 events. Event ids 3 to 5 are kept for NoTenantAllowed, Bypass and ViolationBlocked.
internal static partial class TenantGuardLog
{
    [LoggerMessage(
        EventId = 1,
        EventName = "ModelValidated",
        Level = LogLevel.Information,
        Message = "TenantGuard checked the model: {TenantScopedEntities} tenant-scoped entity types, {Errors} errors, {Warnings} warnings")]
    public static partial void ModelValidated(ILogger logger, int tenantScopedEntities, int errors, int warnings);

    [LoggerMessage(EventId = 2, EventName = "ModelFinding", Message = "{RuleId}: {Finding}")]
    public static partial void ModelFinding(ILogger logger, LogLevel level, string ruleId, string finding);
}
