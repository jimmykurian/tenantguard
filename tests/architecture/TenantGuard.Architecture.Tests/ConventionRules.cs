// <copyright file="ConventionRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TenantGuard.Architecture.Tests.Support;

namespace TenantGuard.Architecture.Tests;

// Conventions from AGENTS.md that hold across assemblies.
public sealed class ConventionRules
{
    // TG001 and TG003 recognize [AllowCrossTenantAccess] by name, since the analyzers can't
    // reference the runtime; renaming or moving the attribute would turn them off silently.
    [Fact]
    public void Analyzers_match_the_AllowCrossTenantAccess_attribute_by_its_real_name()
    {
        var crossTenantAccess = Assembly.LoadFrom(Assemblies.AnalyzersPath).GetType("TenantGuard.Analyzers.CrossTenantAccess", throwOnError: true)!;
        var name = (string?)crossTenantAccess.GetField("AttributeName", BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue();

        typeof(AllowCrossTenantAccessAttribute).Name.Should().Be(name);
        typeof(AllowCrossTenantAccessAttribute).Namespace.Should().Be("TenantGuard");
    }

    // "Interceptors implement both sync and async paths": every SavingChanges-style method an
    // interceptor overrides has its sync or async twin overridden too.
    [Fact]
    public void Interceptors_implement_both_the_sync_and_the_async_path()
    {
        var interceptors = Assemblies.Runtime.GetTypes().Where(type => typeof(IInterceptor).IsAssignableFrom(type) && !type.IsAbstract).ToList();
        interceptors.Should().NotBeEmpty();

        var missing = from interceptor in interceptors
                      let overridden = interceptor.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                          .Where(method => method.GetBaseDefinition().DeclaringType != interceptor)
                          .Select(method => method.Name)
                          .ToHashSet()
                      from name in overridden
                      let twin = name.EndsWith("Async", StringComparison.Ordinal) ? name[..^"Async".Length] : name + "Async"
                      where !overridden.Contains(twin)
                      select $"{interceptor.Name}.{name} has no {twin}";
        missing.Should().BeEmpty();
    }
}
