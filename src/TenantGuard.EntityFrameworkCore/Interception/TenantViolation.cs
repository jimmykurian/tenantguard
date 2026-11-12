// <copyright file="TenantViolation.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard;

/// <summary>
/// One write the TenantGuard write guard blocked.
/// </summary>
/// <param name="Entity">The display name of the entity type; for a write to an owned entity, the owned type's.</param>
/// <param name="State">Whether the row was being added, updated or deleted.</param>
/// <param name="Keys">
/// The row's primary key values by property name. Empty when keys are left out of errors, or not
/// generated yet for a new row.
/// </param>
/// <param name="Expected">The current tenant, or null when there is none.</param>
/// <param name="Actual">
/// The tenant on the row: the new value for an insert, the stored or changed value for an update, the
/// stored value for a delete; for a write to an owned entity, the owner's tenant.
/// </param>
public sealed record TenantViolation(
    string Entity,
    EntityState State,
    IReadOnlyDictionary<string, object?> Keys,
    object? Expected,
    object? Actual)
{
    /// <summary>
    /// Gets the display name of the owner whose tenant was checked, for a write to an owned entity: the
    /// owner that isn't owned. Null for an entity type that isn't owned.
    /// </summary>
    public string? Owner { get; init; }
}
