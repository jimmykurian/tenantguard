// <copyright file="SeededRow.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// A row the probe saved: its key, and every value as saved, shadow ones included, so a check can attach
// a copy of it to another tenant's context.
internal sealed class SeededRow
{
    private SeededRow(IKey key, object?[] keyValues, PropertyValues values)
    {
        Key = key;
        KeyValues = keyValues;
        Values = values;
    }

    public IKey Key { get; }

    public object?[] KeyValues { get; }

    public PropertyValues Values { get; }

    public static SeededRow From(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()!;
        var values = entry.CurrentValues.Clone();
        return new SeededRow(key, [.. key.Properties.Select(property => values[property])], values);
    }

    // The key of a row a query returned, matched by the key's comparers.
    public bool HasKey(EntityEntry entry)
        => Key.Properties.Select((property, index) => property.GetKeyValueComparer().Equals(entry.Property(property.Name).CurrentValue, KeyValues[index])).All(equal => equal);

    // Such as (Id: 7), formatted the way TenantGuard's own messages format key values.
    public string FormatKey()
        => $"({string.Join(", ", Key.Properties.Select((property, index) => $"{property.Name}: {TenantValueFormatter.Format(KeyValues[index])}"))})";
}
