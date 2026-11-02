// <copyright file="FixedTenant.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;
using NSubstitute;

namespace Finbuckle.Api.Tests;

// A Finbuckle context accessor that always holds one tenant, for the contexts the tests create.
internal static class FixedTenant
{
    public static IMultiTenantContextAccessor<TenantInfo> For(string? tenant)
    {
        var context = new MultiTenantContext<TenantInfo>(tenant is null ? null! : new TenantInfo { Id = tenant, Identifier = tenant, Name = tenant });
        var accessor = Substitute.For<IMultiTenantContextAccessor<TenantInfo>>();
        accessor.MultiTenantContext.Returns(context);
        ((IMultiTenantContextAccessor)accessor).MultiTenantContext.Returns(context);
        return accessor;
    }
}
