// <copyright file="ModelCheck.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace TenantGuard.Tool;

/// <summary>
/// Runs the model rules on a context with the options and the context type the EF convention uses.
/// </summary>
internal static class ModelCheck
{
    /// <summary>
    /// Runs the rules on the context's model.
    /// </summary>
    /// <param name="context">The context, created as the app creates it.</param>
    /// <param name="contextType">The context's type, which TGM003 uses (ADR 0015).</param>
    /// <returns>The report, or null when the context uses a compiled model.</returns>
    public static ModelReport? Run(DbContext context, Type contextType)
    {
        var contextOptions = context.GetService<IDbContextOptions>();
        if (contextOptions.FindExtension<CoreOptionsExtension>()?.Model is not null)
        {
            return null;
        }

        var options = contextOptions.FindExtension<TenantGuardOptionsExtension>()?.Options ?? TenantGuardOptions.Default;

        // Under UseTenantGuard and Enforcement.Throw, the convention throws while EF Core builds the
        // model; its report is the one these options and this context type give.
        Microsoft.EntityFrameworkCore.Metadata.IModel model;
        try
        {
            model = context.Model;
        }
        catch (Exception exception) when (FindModelException(exception) is { } modelException)
        {
            return modelException.Report;
        }

        return TenantModelRules.Check(
            model,
            options,
            contextType,
            RelationalProvider.Uses(contextOptions),
            capturedValuesAllowed: PerTenantModel.TenantFor(context, options) is not null).Report;
    }

    /// <summary>
    /// Finds the model exception the convention threw, however EF Core or the activator wrapped it.
    /// </summary>
    /// <param name="exception">The exception caught.</param>
    /// <returns>The model exception, or null when it isn't one.</returns>
    public static TenantGuardModelException? FindModelException(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException)
        {
            if (exception is TenantGuardModelException modelException)
            {
                return modelException;
            }
        }

        return null;
    }
}
