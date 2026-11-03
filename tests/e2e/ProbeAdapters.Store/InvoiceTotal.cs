// <copyright file="InvoiceTotal.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ProbeAdapters.Store;

public sealed class InvoiceTotal
{
    public string TenantId { get; set; } = string.Empty;

    public decimal Total { get; set; }
}
