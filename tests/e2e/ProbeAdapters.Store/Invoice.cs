// <copyright file="Invoice.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ProbeAdapters.Store;

public sealed class Invoice
{
    public int Id { get; set; }

    public string TenantId { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;
}
