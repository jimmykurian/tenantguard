// <copyright file="FixedTenant.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;

namespace Finbuckle.Api.Tests;

// A Finbuckle context accessor that always holds one tenant, for the contexts the tests create.
internal sealed class FixedTenant(string? tenant) : IMultiTenantContextAccessor<TenantInfo>
{
    public IMultiTenantContext<TenantInfo> MultiTenantContext { get; } =
        new MultiTenantContext<TenantInfo>(tenant is null ? null! : new TenantInfo { Id = tenant, Identifier = tenant, Name = tenant });

    IMultiTenantContext IMultiTenantContextAccessor.MultiTenantContext => MultiTenantContext;
}
