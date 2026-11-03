// <copyright file="TenantModelConvention.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;

namespace TenantGuard;

// Runs the rules on the finalized model, stores the per-entity metadata, then throws or logs per
// Enforcement. contextType is the type of the context whose model it is, relational whether its
// provider is relational, and modelTenant the tenant a per-tenant model is built for
// (AllowCapturedValues), or null.
internal sealed class TenantModelConvention(
    TenantGuardOptions options,
    ILogger logger,
    Type contextType,
    bool relational,
    object? modelTenant)
    : IModelFinalizedConvention
{
    public IModel ProcessModelFinalized(IModel model)
    {
        var check = TenantModelRules.Check(model, options, contextType, relational, capturedValuesAllowed: modelTenant is not null);
        model.GetOrAddRuntimeAnnotationValue(
            TenantGuardEntities.AnnotationName,
            static arguments => TenantGuardEntities.Create(arguments.Model, arguments.TenantProperties, arguments.ModelTenant),
            (Model: model, check.TenantProperties, ModelTenant: modelTenant));

        var report = check.Report;
        TenantGuardLog.ModelValidated(logger, check.TenantProperties.Count, report.Errors.Count, report.Warnings.Count);
        foreach (var warning in report.Warnings)
        {
            TenantGuardLog.ModelFinding(logger, LogLevel.Warning, warning.RuleId, warning.Message);
        }

        if (report.Errors.Count > 0)
        {
            if (options.Enforcement == Enforcement.Throw)
            {
                throw new TenantGuardModelException(report);
            }

            foreach (var error in report.Errors)
            {
                TenantGuardLog.ModelFinding(logger, LogLevel.Error, error.RuleId, error.Message);
            }
        }

        return model;
    }
}
