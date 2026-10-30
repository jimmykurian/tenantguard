// <copyright file="TenantPropertyMetadata.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// A tenant property with what the write guard needs at save time: its comparer (SPEC 5), and the
// sentinel EF treats as "not set".
internal readonly record struct TenantPropertyMetadata(IProperty Property, ValueComparer Comparer, object? Sentinel)
{
    public bool Matches(object? value, object? tenant) => Comparer.Equals(value, tenant);

    public bool IsUnset(object? value) => value is null || Comparer.Equals(value, Sentinel);
}
