// <copyright file="Invoice.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

// IsMultiTenant() gives it a shadow TenantId.
internal sealed class Invoice
{
    public int Id { get; set; }

    public required string Number { get; set; }

    public int CustomerId { get; set; }
}
