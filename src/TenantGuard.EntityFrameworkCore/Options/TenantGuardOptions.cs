// <copyright file="TenantGuardOptions.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;

namespace TenantGuard;

/// <summary>
/// The TenantGuard configuration. Build one with <see cref="TenantGuardOptionsBuilder"/>.
/// </summary>
public sealed class TenantGuardOptions
{
    internal static readonly TenantGuardOptions Default = new(
        ["TenantId"],
        ImmutableDictionary<Type, string>.Empty,
        ImmutableHashSet<Type>.Empty,
        requireAllEntitiesScoped: false,
        Enforcement.Throw);

    private TenantGuardOptions(
        ImmutableArray<string> tenantPropertyNames,
        ImmutableDictionary<Type, string> explicitTenantProperties,
        ImmutableHashSet<Type> sharedTypes,
        bool requireAllEntitiesScoped,
        Enforcement enforcement)
    {
        TenantPropertyNames = tenantPropertyNames;
        ExplicitTenantProperties = explicitTenantProperties;
        SharedTypes = sharedTypes;
        RequireAllEntitiesScoped = requireAllEntitiesScoped;
        Enforcement = enforcement;
    }

    internal ImmutableArray<string> TenantPropertyNames { get; }

    internal ImmutableDictionary<Type, string> ExplicitTenantProperties { get; }

    internal ImmutableHashSet<Type> SharedTypes { get; }

    internal bool RequireAllEntitiesScoped { get; }

    internal Enforcement Enforcement { get; }

    internal TenantGuardOptions WithTenantPropertyNames(ImmutableArray<string> names)
        => With(tenantPropertyNames: names);

    internal TenantGuardOptions WithTenantProperty(Type entityType, string propertyName)
        => With(explicitTenantProperties: ExplicitTenantProperties.SetItem(entityType, propertyName));

    internal TenantGuardOptions WithSharedType(Type entityType)
        => With(sharedTypes: SharedTypes.Add(entityType));

    internal TenantGuardOptions WithRequireAllEntitiesScoped()
        => With(requireAllEntitiesScoped: true);

    internal TenantGuardOptions WithEnforcement(Enforcement enforcement)
        => With(enforcement: enforcement);

    // EF keys its internal service provider, and with it the model cache, on these two.
    internal bool IsEquivalentTo(TenantGuardOptions other)
        => TenantPropertyNames.SequenceEqual(other.TenantPropertyNames, StringComparer.Ordinal)
            && ExplicitTenantProperties.Count == other.ExplicitTenantProperties.Count
            && ExplicitTenantProperties.All(pair => other.ExplicitTenantProperties.TryGetValue(pair.Key, out var name) && name == pair.Value)
            && SharedTypes.SetEquals(other.SharedTypes)
            && RequireAllEntitiesScoped == other.RequireAllEntitiesScoped
            && Enforcement == other.Enforcement;

    internal int GetEquivalenceHashCode()
    {
        var hash = default(HashCode);
        foreach (var name in TenantPropertyNames)
        {
            hash.Add(name, StringComparer.Ordinal);
        }

        hash.Add(ExplicitTenantProperties.Count);
        hash.Add(SharedTypes.Count);
        hash.Add(RequireAllEntitiesScoped);
        hash.Add(Enforcement);
        return hash.ToHashCode();
    }

    private TenantGuardOptions With(
        ImmutableArray<string>? tenantPropertyNames = null,
        ImmutableDictionary<Type, string>? explicitTenantProperties = null,
        ImmutableHashSet<Type>? sharedTypes = null,
        bool? requireAllEntitiesScoped = null,
        Enforcement? enforcement = null)
        => new(
            tenantPropertyNames ?? TenantPropertyNames,
            explicitTenantProperties ?? ExplicitTenantProperties,
            sharedTypes ?? SharedTypes,
            requireAllEntitiesScoped ?? RequireAllEntitiesScoped,
            enforcement ?? Enforcement);
}
