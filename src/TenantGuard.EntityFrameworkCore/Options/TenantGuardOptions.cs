// <copyright file="TenantGuardOptions.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard;

/// <summary>
/// The TenantGuard configuration. Build one with <see cref="TenantGuardOptionsBuilder"/>.
/// </summary>
public sealed class TenantGuardOptions
{
    internal static readonly TenantGuardOptions Default = new();

    private TenantGuardOptions()
    {
    }

    internal ImmutableArray<string> TenantPropertyNames { get; private set; } = ["TenantId"];

    internal ImmutableDictionary<Type, string> ExplicitTenantProperties { get; private set; } = ImmutableDictionary<Type, string>.Empty;

    internal ImmutableHashSet<Type> SharedTypes { get; private set; } = ImmutableHashSet<Type>.Empty;

    internal bool RequireAllEntitiesScoped { get; private set; }

    internal bool AllowCapturedValues { get; private set; }

    // Throw, the first value, is the default.
    internal Enforcement Enforcement { get; private set; }

    // The write guard's settings. EF reads them per context instance, so they stay out of the
    // equivalence below, and a CurrentTenant lambda that captures state costs no new service provider.
    internal Func<DbContext, object?>? CurrentTenant { get; private set; }

    // Reject, the first value, is the default.
    internal NoTenantPolicy NoTenantPolicy { get; private set; }

    internal bool StampOnAdd { get; private set; }

    internal bool IncludeKeysInErrors { get; private set; } = true;

    internal TenantGuardOptions WithTenantPropertyNames(ImmutableArray<string> names)
        => With(options => options.TenantPropertyNames = names);

    internal TenantGuardOptions WithTenantProperty(Type entityType, string propertyName)
        => With(options => options.ExplicitTenantProperties = options.ExplicitTenantProperties.SetItem(entityType, propertyName));

    internal TenantGuardOptions WithSharedType(Type entityType)
        => With(options => options.SharedTypes = options.SharedTypes.Add(entityType));

    internal TenantGuardOptions WithRequireAllEntitiesScoped()
        => With(options => options.RequireAllEntitiesScoped = true);

    internal TenantGuardOptions WithAllowCapturedValues()
        => With(options => options.AllowCapturedValues = true);

    internal TenantGuardOptions WithEnforcement(Enforcement enforcement)
        => With(options => options.Enforcement = enforcement);

    internal TenantGuardOptions WithCurrentTenant(Func<DbContext, object?> resolver)
        => With(options => options.CurrentTenant = resolver);

    internal TenantGuardOptions WithNoTenantPolicy(NoTenantPolicy policy)
        => With(options => options.NoTenantPolicy = policy);

    internal TenantGuardOptions WithStampOnAdd(bool stampOnAdd)
        => With(options => options.StampOnAdd = stampOnAdd);

    internal TenantGuardOptions WithIncludeKeysInErrors(bool includeKeys)
        => With(options => options.IncludeKeysInErrors = includeKeys);

    // EF keys its internal service provider, and with it the model cache, on these two. Only the
    // settings that shape the model take part.
    internal bool IsEquivalentTo(TenantGuardOptions other)
        => TenantPropertyNames.SequenceEqual(other.TenantPropertyNames, StringComparer.Ordinal)
            && ExplicitTenantProperties.Count == other.ExplicitTenantProperties.Count
            && ExplicitTenantProperties.All(pair => other.ExplicitTenantProperties.TryGetValue(pair.Key, out var name) && name == pair.Value)
            && SharedTypes.SetEquals(other.SharedTypes)
            && RequireAllEntitiesScoped == other.RequireAllEntitiesScoped
            && AllowCapturedValues == other.AllowCapturedValues
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
        hash.Add(AllowCapturedValues);
        hash.Add(Enforcement);
        return hash.ToHashCode();
    }

    private TenantGuardOptions With(Action<TenantGuardOptions> change)
    {
        var copy = (TenantGuardOptions)MemberwiseClone();
        change(copy);
        return copy;
    }
}
