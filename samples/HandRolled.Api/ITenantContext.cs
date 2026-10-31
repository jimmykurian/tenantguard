// <copyright file="ITenantContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

// The tenant the current request acts for. The app owns it; TenantGuard only reads it through the context.
internal interface ITenantContext
{
    string? TenantId { get; }
}
