// <copyright file="TenantContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

// One per request, set from the X-Tenant header (Program.cs), or directly by the smoke run and the tests.
internal sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; set; }
}
