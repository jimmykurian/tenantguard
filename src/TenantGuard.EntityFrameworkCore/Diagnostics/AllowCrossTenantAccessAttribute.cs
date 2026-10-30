// <copyright file="AllowCrossTenantAccessAttribute.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// Marks a method or class that reads or writes across tenants on purpose, which clears analyzers
/// TG001, TG002 and TG004 inside it. The reason is required: one that is empty is TG003.
/// </summary>
/// <param name="reason">Why the code crosses tenants, such as "nightly billing across tenants".</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class AllowCrossTenantAccessAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets why the code crosses tenants.
    /// </summary>
    public string Reason { get; } = reason;
}
