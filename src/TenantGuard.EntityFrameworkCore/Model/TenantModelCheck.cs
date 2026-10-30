// <copyright file="TenantModelCheck.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

// The findings, plus the tenant property of each tenant-scoped entity type by entity type name.
internal sealed record TenantModelCheck(ModelReport Report, IReadOnlyDictionary<string, string> TenantProperties);
