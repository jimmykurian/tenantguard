// <copyright file="Customer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

// IsMultiTenant() gives it a shadow TenantId.
internal sealed class Customer
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
