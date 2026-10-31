// <copyright file="Customer.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

internal sealed class Customer
{
    public int Id { get; set; }

    public required string TenantId { get; set; }

    public required string Name { get; set; }
}
