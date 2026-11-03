// <copyright file="Note.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

// Indexed on the tenant, so TGM009 stays out of these tests' output.
[Index(nameof(TenantId))]
public sealed class Note
{
    public int Id { get; set; }

    public string? TenantId { get; set; }
}
