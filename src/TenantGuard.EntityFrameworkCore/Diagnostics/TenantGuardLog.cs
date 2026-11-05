// <copyright file="TenantGuardLog.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Extensions.Logging;

namespace TenantGuard;

// TenantGuard's log events.
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

    [LoggerMessage(
        EventId = 3,
        EventName = "NoTenantAllowed",
        Level = LogLevel.Information,
        Message = "TenantGuard let {Context} save without a current tenant (NoTenantPolicy.Allow)")]
    public static partial void NoTenantAllowed(ILogger logger, string context);

    [LoggerMessage(EventId = 4, EventName = "Bypass", Level = LogLevel.Warning, Message = "TenantGuard bypass: {Reason} ({Context})")]
    public static partial void Bypass(ILogger logger, string reason, string context);

    [LoggerMessage(
        EventId = 5,
        EventName = "ViolationBlocked",
        Level = LogLevel.Error,
        Message = "TenantGuard blocked SaveChanges on {Context}; cross-tenant writes: {Count}")]
    public static partial void ViolationBlocked(ILogger logger, string context, int count);

    [LoggerMessage(
        EventId = 6,
        EventName = "UnfilteredEntity",
        Level = LogLevel.Information,
        Message = "TenantGuard left {Entity} unfiltered, as declared: {Reason}")]
    public static partial void UnfilteredEntity(ILogger logger, string entity, string reason);

    [LoggerMessage(
        EventId = 7,
        EventName = "CrossTenantModel",
        Level = LogLevel.Information,
        Message = "TenantGuard skipped the rules for {Context}, declared cross-tenant: {Reason}")]
    public static partial void CrossTenantModel(ILogger logger, string context, string reason);

    [LoggerMessage(
        EventId = 8,
        EventName = "FindingSuppressed",
        Level = LogLevel.Debug,
        Message = "{RuleId} suppressed for {Entity}: {Reason}")]
    public static partial void FindingSuppressed(ILogger logger, string ruleId, string entity, string reason);
}
