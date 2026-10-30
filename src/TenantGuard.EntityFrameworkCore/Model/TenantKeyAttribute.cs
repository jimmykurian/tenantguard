// <copyright file="TenantKeyAttribute.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// Marks the property that holds an entity's tenant key, for a property whose name is not one of
/// the configured tenant property names.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class TenantKeyAttribute : Attribute
{
}
