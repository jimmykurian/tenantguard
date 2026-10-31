// <copyright file="ProbeTarget.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// A tenant-scoped entity type the probe seeded, with its row for each tenant.
internal sealed record ProbeTarget(IEntityType Type, EntityQueries Queries, IReadOnlyDictionary<string, SeededRow> Rows)
{
    public string Name => Type.DisplayName();
}
