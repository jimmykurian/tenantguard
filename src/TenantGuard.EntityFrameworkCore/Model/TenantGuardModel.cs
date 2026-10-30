// <copyright file="TenantGuardModel.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

/// <summary>
/// Runs the TenantGuard model rules on a model directly, for unit tests and tooling.
/// </summary>
public static class TenantGuardModel
{
    /// <summary>
    /// Runs every TenantGuard model rule on <paramref name="model"/>.
    /// </summary>
    /// <param name="model">The model to check, such as <c>context.Model</c>.</param>
    /// <param name="options">The options, from <see cref="TenantGuardOptionsBuilder.Options"/>.</param>
    /// <returns>The findings. Nothing is thrown or logged, whatever the options say about enforcement.</returns>
    public static ModelReport Validate(IModel model, TenantGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(options);

        return TenantModelRules.Check(model, options).Report;
    }
}
