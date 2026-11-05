// <copyright file="Membership.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

// Has a tenant column and no filter on purpose: DeclaredDbContext declares it Unfiltered.
[Index(nameof(TenantId))]
public sealed class Membership
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;
}
