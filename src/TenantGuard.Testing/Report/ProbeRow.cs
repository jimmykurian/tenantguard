// <copyright file="ProbeRow.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

/// <summary>
/// One check of one entity type, across every pair of tenants.
/// </summary>
/// <param name="Entity">The display name of the entity type.</param>
/// <param name="Check">
/// ReadIsolation, FindIsolation, BulkIsolation, UpdateGuard, InsertGuard or DeleteGuard.
/// </param>
/// <param name="Outcome">Passed, Failed or Skipped.</param>
/// <param name="Detail">What leaked, or why the check was skipped. Empty when it passed.</param>
public sealed record ProbeRow(string Entity, string Check, ProbeOutcome Outcome, string Detail);
