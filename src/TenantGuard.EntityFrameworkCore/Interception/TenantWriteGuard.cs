// <copyright file="TenantWriteGuard.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace TenantGuard;

// Checks every Added, Modified and Deleted entry of a tenant-scoped entity type against the current
// tenant (SPEC 5), and throws before SaveChanges reaches the database.
internal static class TenantWriteGuard
{
    public static void Check(DbContext? context, TenantGuardScope scope)
    {
        if (context is null || scope.IsBypassed)
        {
            return;
        }

        // Nothing allocates while the model has no tenant-scoped entity type or nothing is tracked (SPEC 13).
        var entities = scope.GetEntities(context);
        if (entities.IsEmpty || !HasEntries(context.ChangeTracker))
        {
            return;
        }

        var options = scope.Options;
        object? currentTenant = null;
        var resolved = false;
        List<Blocked>? blocked = null;
        List<PropertyEntry>? stamps = null;

        // Entries() runs DetectChanges first: SavingChanges comes before SaveChanges runs it.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || !entities.TryGet(entry.Metadata, out var tenant))
            {
                continue;
            }

            if (!resolved)
            {
                currentTenant = ResolveCurrentTenant(context, options);
                resolved = true;
                if (currentTenant is null && options.NoTenantPolicy == NoTenantPolicy.Allow)
                {
                    TenantGuardLog.NoTenantAllowed(scope.Logger, context.GetType().Name);
                    return;
                }
            }

            if (currentTenant is not null && !tenant.Property.ClrType.IsInstanceOfType(currentTenant))
            {
                throw new InvalidOperationException(
                    $"CurrentTenant returned a {currentTenant.GetType().Name}, but {entry.Metadata.DisplayName()}.{tenant.Property.Name} is a {tenant.Property.ClrType.Name}. Return a value of the tenant property's type.");
            }

            var property = entry.Property(tenant.Property);
            if (entry.State == EntityState.Added && currentTenant is not null && options.StampOnAdd && tenant.IsUnset(property.CurrentValue))
            {
                (stamps ??= []).Add(property);
            }
            else if (CheckEntry(entry, property, tenant, currentTenant, options) is { } write)
            {
                (blocked ??= []).Add(write);
            }
        }

        if (blocked is not null)
        {
            TenantGuardLog.ViolationBlocked(scope.Logger, context.GetType().Name, blocked.Count);
            throw new TenantIsolationViolationException(
                [.. blocked.Select(write => write.Violation)],
                [.. blocked.Select(write => write.Detail)]);
        }

        // Stamped only once every entry passed, so a blocked save leaves the entities as they were.
        if (stamps is not null)
        {
            foreach (var stamp in stamps)
            {
                stamp.CurrentValue = currentTenant;
            }
        }
    }

    // DetectChanges allocates even on an empty change tracker, and finds nothing there to add, so
    // this looks without it.
    private static bool HasEntries(ChangeTracker changeTracker)
    {
        var autoDetectChanges = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            using var entries = changeTracker.Entries().GetEnumerator();
            return entries.MoveNext();
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    private static object? ResolveCurrentTenant(DbContext context, TenantGuardOptions options)
        => options.CurrentTenant is { } resolve
            ? resolve(context)
            : throw new InvalidOperationException(
                $"TenantGuard checks the writes of {context.GetType().Name} against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.");

    private static Blocked? CheckEntry(
        EntityEntry entry,
        PropertyEntry property,
        TenantPropertyMetadata tenant,
        object? currentTenant,
        TenantGuardOptions options)
    {
        var name = tenant.Property.Name;
        if (entry.State == EntityState.Added)
        {
            var value = property.CurrentValue;
            return currentTenant is not null && tenant.Matches(value, currentTenant)
                ? null
                : Block(entry, options, currentTenant, value, $"{name} is {TenantValueFormatter.Format(value)}");
        }

        // Modified or Deleted: the stored row must be the current tenant's, and an update must not move it.
        var original = property.OriginalValue;
        if (currentTenant is null || !tenant.Matches(original, currentTenant))
        {
            return Block(entry, options, currentTenant, original, $"{name} is {TenantValueFormatter.Format(original)}");
        }

        var changed = property.CurrentValue;
        return entry.State == EntityState.Modified && !tenant.Matches(changed, currentTenant)
            ? Block(entry, options, currentTenant, changed, $"{name} changes from {TenantValueFormatter.Format(original)} to {TenantValueFormatter.Format(changed)}")
            : null;
    }

    private static Blocked Block(EntityEntry entry, TenantGuardOptions options, object? expected, object? actual, string detail)
        => new(new TenantViolation(entry.Metadata.DisplayName(), entry.State, Keys(entry, options), expected, actual), detail);

    // Primary key values, leaving out temporary ones EF generated for a row it has not saved yet.
    private static ReadOnlyDictionary<string, object?> Keys(EntityEntry entry, TenantGuardOptions options)
    {
        if (!options.IncludeKeysInErrors || entry.Metadata.FindPrimaryKey() is not { } key)
        {
            return ReadOnlyDictionary<string, object?>.Empty;
        }

        Dictionary<string, object?>? keys = null;
        foreach (var keyProperty in key.Properties)
        {
            var value = entry.Property(keyProperty);
            if (!value.IsTemporary)
            {
                (keys ??= new Dictionary<string, object?>(StringComparer.Ordinal))[keyProperty.Name] = value.CurrentValue;
            }
        }

        return keys is null ? ReadOnlyDictionary<string, object?>.Empty : keys.AsReadOnly();
    }

    // A blocked write, and how the message describes its tenant property: TenantId is "globex".
    private readonly record struct Blocked(TenantViolation Violation, string Detail);
}
