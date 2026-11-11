// <copyright file="TenantModelConvention.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;

namespace TenantGuard;

// Runs the rules on the finalized model, stores the per-entity metadata, then throws or logs per
// Enforcement. contextType is the type of the context whose model it is, relational whether its
// provider is relational, modelTenant the tenant a per-tenant model is built for
// (AllowCapturedValues), or null, and noTenantModel whether it is the no-tenant model of a model per
// tenant (CrossTenantWhenNoTenant).
internal sealed class TenantModelConvention(
    TenantGuardOptions options,
    ILogger logger,
    Type contextType,
    bool relational,
    object? modelTenant,
    bool noTenantModel)
    : IModelFinalizedConvention
{
    public IModel ProcessModelFinalized(IModel model)
    {
        var check = TenantModelRules.Check(
            model,
            options,
            contextType,
            relational,
            capturedValuesAllowed: modelTenant is not null,
            noTenantModel: noTenantModel);
        model.GetOrAddRuntimeAnnotationValue(
            TenantGuardEntities.AnnotationName,
            static arguments => TenantGuardEntities.Create(arguments.Model, arguments.Check, arguments.ModelTenant),
            (Model: model, Check: check, ModelTenant: modelTenant));

        var report = check.Report;

        // The validate tool prints the check itself, so it isn't logged twice.
        var log = !ValidateToolSwitch.IsOn;
        if (report.CrossTenantReason is { } crossTenantReason)
        {
            if (log)
            {
                TenantGuardLog.CrossTenantModel(logger, contextType.Name, crossTenantReason);
            }

            return model;
        }

        CheckDeclarations(model);
        if (report.Errors.Count > 0 && options.Enforcement == Enforcement.Throw)
        {
            LogCheck(report, check.TenantProperties.Count, errors: false, log);
            throw new TenantGuardModelException(report);
        }

        LogCheck(report, check.TenantProperties.Count, errors: true, log);
        return model;
    }

    // The declarations, the summary, the warnings and suppressed findings, and the errors when they
    // aren't thrown.
    private void LogCheck(ModelReport report, int tenantScopedEntities, bool errors, bool log)
    {
        if (!log)
        {
            return;
        }

        foreach (var unfiltered in report.Unfiltered)
        {
            TenantGuardLog.UnfilteredEntity(logger, unfiltered.Entity, unfiltered.Reason);
        }

        TenantGuardLog.ModelValidated(logger, tenantScopedEntities, report.Errors.Count, report.Warnings.Count);
        foreach (var warning in report.Warnings)
        {
            TenantGuardLog.ModelFinding(logger, LogLevel.Warning, warning.RuleId, warning.Message);
        }

        foreach (var suppressed in report.SuppressedWithReasons)
        {
            TenantGuardLog.FindingSuppressed(logger, suppressed.Finding.RuleId, suppressed.Finding.Entity, suppressed.Reason);
        }

        foreach (var error in errors ? report.Errors : [])
        {
            TenantGuardLog.ModelFinding(logger, LogLevel.Error, error.RuleId, error.Message);
        }
    }

    // An Unfiltered declaration names a root entity type with a tenant property; a type the model
    // doesn't map is left alone, so contexts can share one configuration.
    private void CheckDeclarations(IModel model)
    {
        foreach (var clrType in options.UnfilteredTypes.Keys)
        {
            if (model.FindEntityType(clrType) is not { } entityType)
            {
                continue;
            }

            var name = entityType.DisplayName();
            if (entityType.IsOwned() || entityType.BaseType is not null)
            {
                var root = entityType.IsOwned()
                    ? entityType.FindOwnership()?.PrincipalEntityType.DisplayName()
                    : entityType.GetRootType().DisplayName();
                throw new InvalidOperationException(
                    $"Unfiltered<{name}>() names a derived or owned type. Declare the root of its hierarchy, or its owner, {root}.");
            }

            if (TenantPropertyDiscovery.Find(entityType, options) is null)
            {
                throw new InvalidOperationException(
                    $"Unfiltered<{name}>() names a type with no tenant property. Declare a tenant-free type with Shared<{name}>().");
            }
        }
    }
}
