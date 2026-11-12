// <copyright file="OwnerEntries.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Finds the owner of an owned entry from its ownership foreign key, among the tracked entries. Built
// only when an owned entry changed; each principal key's index is built the first time it is needed.
internal sealed class OwnerEntries(ChangeTracker changeTracker)
{
    private readonly Dictionary<IKey, Dictionary<object?[], EntityEntry>> _byKey = [];

    // The owner that isn't owned, walking up through nested owned types; null if one isn't tracked,
    // which EF Core doesn't allow.
    public EntityEntry? RootOwner(EntityEntry entry)
    {
        var current = entry;
        while (current.Metadata.FindOwnership() is { } ownership)
        {
            var values = new object?[ownership.Properties.Count];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = current.Property(ownership.Properties[i]).CurrentValue;
            }

            if (!Index(ownership.PrincipalKey).TryGetValue(values, out var owner))
            {
                return null;
            }

            current = owner;
        }

        return current;
    }

    private Dictionary<object?[], EntityEntry> Index(IKey key)
    {
        if (_byKey.TryGetValue(key, out var index))
        {
            return index;
        }

        index = new Dictionary<object?[], EntityEntry>(new KeyValuesComparer(key));

        // The save's first Entries() already ran DetectChanges.
        var autoDetectChanges = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            foreach (var entry in changeTracker.Entries())
            {
                if (key.DeclaringEntityType.IsAssignableFrom(entry.Metadata))
                {
                    var values = new object?[key.Properties.Count];
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = entry.Property(key.Properties[i]).CurrentValue;
                    }

                    index.TryAdd(values, entry);
                }
            }
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

        _byKey[key] = index;
        return index;
    }

    // Compares key values with each key property's key comparer, as EF Core does.
    private sealed class KeyValuesComparer(IKey key) : IEqualityComparer<object?[]>
    {
        private readonly ValueComparer[] _comparers = [.. key.Properties.Select(property => property.GetKeyValueComparer())];

        public bool Equals(object?[]? x, object?[]? y)
        {
            if (x is null || y is null)
            {
                return x == y;
            }

            for (var i = 0; i < _comparers.Length; i++)
            {
                if (!_comparers[i].Equals(x[i], y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object?[] obj)
        {
            var hash = default(HashCode);
            for (var i = 0; i < _comparers.Length; i++)
            {
                hash.Add(obj[i] is null ? 0 : _comparers[i].GetHashCode(obj[i]));
            }

            return hash.ToHashCode();
        }
    }
}
