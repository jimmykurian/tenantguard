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

    // Cross-tenant data declared on purpose, each with its reason (docs/reference/options.md#cross-tenant-data).
    internal ImmutableDictionary<Type, string> UnfilteredTypes { get; private set; } = ImmutableDictionary<Type, string>.Empty;

    internal string? CrossTenantReason { get; private set; }

    internal string? CrossTenantWhenNoTenantReason { get; private set; }

    internal bool NullTenantIsHost { get; private set; }

    // Rule ID to reason, for every entity type.
    internal ImmutableDictionary<string, string> Suppressions { get; private set; } = ImmutableDictionary<string, string>.Empty;

    // (entity type, rule ID) to reason.
    internal ImmutableDictionary<(Type EntityType, string RuleId), string> TypeSuppressions { get; private set; } =
        ImmutableDictionary<(Type EntityType, string RuleId), string>.Empty;

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

    internal TenantGuardOptions WithUnfiltered(Type entityType, string reason)
        => With(options => options.UnfilteredTypes = options.UnfilteredTypes.SetItem(entityType, reason));

    internal TenantGuardOptions WithCrossTenant(string reason)
        => With(options => options.CrossTenantReason = reason);

    internal TenantGuardOptions WithCrossTenantWhenNoTenant(string reason)
        => With(options => options.CrossTenantWhenNoTenantReason = reason);

    internal TenantGuardOptions WithNullTenantIsHost()
        => With(options => options.NullTenantIsHost = true);

    internal TenantGuardOptions WithSuppression(string ruleId, string reason)
        => With(options => options.Suppressions = options.Suppressions.SetItem(ruleId, reason));

    internal TenantGuardOptions WithSuppression(Type entityType, string ruleId, string reason)
        => With(options => options.TypeSuppressions = options.TypeSuppressions.SetItem((entityType, ruleId), reason));

    // The reason a finding of ruleId on entityType is suppressed, or null.
    internal string? SuppressionReason(Type entityType, string ruleId)
    {
        if (TypeSuppressions.TryGetValue((entityType, ruleId), out var reason))
        {
            return reason;
        }

        return Suppressions.GetValueOrDefault(ruleId);
    }

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
            && SameEntries(ExplicitTenantProperties, other.ExplicitTenantProperties)
            && SharedTypes.SetEquals(other.SharedTypes)
            && RequireAllEntitiesScoped == other.RequireAllEntitiesScoped
            && AllowCapturedValues == other.AllowCapturedValues
            && Enforcement == other.Enforcement
            && SameEntries(UnfilteredTypes, other.UnfilteredTypes)
            && CrossTenantReason == other.CrossTenantReason
            && CrossTenantWhenNoTenantReason == other.CrossTenantWhenNoTenantReason
            && NullTenantIsHost == other.NullTenantIsHost
            && SameEntries(Suppressions, other.Suppressions)
            && SameEntries(TypeSuppressions, other.TypeSuppressions);

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
        hash.Add(UnfilteredTypes.Count);
        hash.Add(CrossTenantReason, StringComparer.Ordinal);
        hash.Add(CrossTenantWhenNoTenantReason, StringComparer.Ordinal);
        hash.Add(NullTenantIsHost);
        hash.Add(Suppressions.Count);
        hash.Add(TypeSuppressions.Count);
        return hash.ToHashCode();
    }

    private static bool SameEntries<TKey>(ImmutableDictionary<TKey, string> first, ImmutableDictionary<TKey, string> second)
        where TKey : notnull
        => first.Count == second.Count
            && first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private TenantGuardOptions With(Action<TenantGuardOptions> change)
    {
        var copy = (TenantGuardOptions)MemberwiseClone();
        change(copy);
        return copy;
    }
}
