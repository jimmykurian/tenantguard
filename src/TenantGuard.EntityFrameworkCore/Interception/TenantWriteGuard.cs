// <copyright file="TenantWriteGuard.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Checks every Added, Modified and Deleted entry of a tenant-scoped entity type against the current
// tenant, and an owned one through its owner, and throws before SaveChanges reaches the database.
internal static class TenantWriteGuard
{
    public static void Check(DbContext? context, TenantGuardScope scope)
    {
        if (context is null || scope.IsBypassed)
        {
            return;
        }

        // Nothing allocates while the model has no tenant-scoped entity type or nothing is tracked. A
        // cross-tenant context's model has none.
        var entities = scope.GetEntities(context);
        if (entities.IsEmpty || entities.CrossTenant == CrossTenantModel.Context || !HasEntries(context.ChangeTracker))
        {
            return;
        }

        var options = scope.Options;
        if (entities.CrossTenant == CrossTenantModel.NoTenant)
        {
            CheckNoTenantModel(context, entities, scope, options);
            return;
        }

        var host = options.NullTenantIsHost;
        object? currentTenant = null;
        var resolved = false;
        List<Blocked>? blocked = null;
        List<PropertyEntry>? stamps = null;
        List<EntityEntry>? owned = null;

        // Reads the current tenant once, at the first tenant-scoped write; false when NoTenantPolicy.Allow
        // lets the save through unchecked.
        bool Resolve()
        {
            if (resolved)
            {
                return true;
            }

            currentTenant = ResolveCurrentTenant(context, options);
            resolved = true;

            // Under NullTenantIsHost, no current tenant is the host, checked like any tenant.
            if (currentTenant is null && !host && options.NoTenantPolicy == NoTenantPolicy.Allow)
            {
                TenantGuardLog.NoTenantAllowed(scope.Logger, context.GetType().Name);
                return false;
            }

            // A per-tenant model handed to another tenant's context: what it read came from the model's tenant.
            if (currentTenant is not null && entities.ModelTenant is { } modelTenant && !Equals(modelTenant, currentTenant))
            {
                TenantGuardLog.ViolationBlocked(scope.Logger, context.GetType().Name, 0);
                throw new TenantIsolationViolationException(context.GetType().Name, modelTenant, currentTenant);
            }

            return true;
        }

        // Entries() runs DetectChanges first: SavingChanges comes before SaveChanges runs it.
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (!entities.TryGet(entry.Metadata, out var tenant))
            {
                // Checked through its owner once every entry is seen ([0014]).
                if (entry.Metadata.IsOwned())
                {
                    (owned ??= []).Add(entry);
                }

                continue;
            }

            if (tenant.Unfiltered)
            {
                continue;
            }

            if (!Resolve())
            {
                return;
            }

            CheckTenantType(currentTenant, entry.Metadata, tenant);
            var property = entry.Property(tenant.Property);
            if (entry.State == EntityState.Added && currentTenant is not null && options.StampOnAdd && tenant.IsUnset(property.CurrentValue))
            {
                (stamps ??= []).Add(property);
            }
            else if (CheckEntry(entry, property, tenant, currentTenant, host, options) is { } write)
            {
                (blocked ??= []).Add(write);
            }
        }

        if (owned is not null)
        {
            var owners = new OwnerEntries(context.ChangeTracker);
            foreach (var entry in owned)
            {
                if (!entities.TryGet(OwnerEntries.RootType(entry.Metadata), out var tenant) || tenant.Unfiltered)
                {
                    continue;
                }

                if (!Resolve())
                {
                    return;
                }

                CheckTenantType(currentTenant, OwnerEntries.RootType(entry.Metadata), tenant);
                if (CheckOwned(entry, owners, tenant, currentTenant, host, options) is { } write)
                {
                    (blocked ??= []).Add(write);
                }
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

    // CrossTenantWhenNoTenant: the no-tenant model's saves aren't checked, but one with a current tenant
    // means that tenant's context was handed the no-tenant model, whose filters read every tenant's rows.
    private static void CheckNoTenantModel(DbContext context, TenantGuardEntities entities, TenantGuardScope scope, TenantGuardOptions options)
    {
        var writesTenantRows = context.ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && entities.TryGet(OwnerEntries.RootType(entry.Metadata), out var tenant)
            && !tenant.Unfiltered);
        if (writesTenantRows && ResolveCurrentTenant(context, options) is { } currentTenant)
        {
            TenantGuardLog.ViolationBlocked(scope.Logger, context.GetType().Name, 0);
            throw new TenantIsolationViolationException(context.GetType().Name, currentTenant);
        }
    }

    private static object? ResolveCurrentTenant(DbContext context, TenantGuardOptions options)
        => options.CurrentTenant is { } resolve
            ? resolve(context)
            : throw new InvalidOperationException(
                $"TenantGuard checks the writes of {context.GetType().Name} against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.");

    private static void CheckTenantType(object? currentTenant, IReadOnlyEntityType entityType, TenantPropertyMetadata tenant)
    {
        if (currentTenant is not null && !tenant.Property.ClrType.IsInstanceOfType(currentTenant))
        {
            throw new InvalidOperationException(
                $"CurrentTenant returned a {currentTenant.GetType().Name}, but {entityType.DisplayName()}.{tenant.Property.Name} is a {tenant.Property.ClrType.Name}. Return a value of the tenant property's type.");
        }
    }

    // An owned entry, through the owner it is written to and, for an update or a delete, the one it was
    // stored under, which differ when a row moves to another owner. An owner that is itself written is
    // checked by its own entry; an Unchanged one's stored row has to be the current tenant's. An owner
    // that isn't tracked can't be checked, so the write is blocked.
    private static Blocked? CheckOwned(
        EntityEntry entry,
        OwnerEntries owners,
        TenantPropertyMetadata tenant,
        object? currentTenant,
        bool host,
        TenantGuardOptions options)
    {
        var ownerName = OwnerEntries.RootType(entry.Metadata).DisplayName();
        var name = tenant.Property.Name;
        Blocked Blocked(object? actual, string detail)
            => new(new TenantViolation(entry.Metadata.DisplayName(), entry.State, Keys(entry, options), currentTenant, actual) { Owner = ownerName }, detail);

        for (var pass = 0; pass < 2; pass++)
        {
            var original = pass == 1;
            if (original ? entry.State == EntityState.Added : entry.State == EntityState.Deleted)
            {
                continue;
            }

            if (owners.RootOwner(entry, original) is not { } owner)
            {
                return Blocked(null, $"owned by {ownerName}, which isn't tracked, so its {name} can't be checked");
            }

            if (owner.State != EntityState.Unchanged)
            {
                continue;
            }

            var value = owner.Property(tenant.Property).OriginalValue;
            if (currentTenant is not null ? !tenant.Matches(value, currentTenant) : !(host && value is null))
            {
                return Blocked(value, $"owned by {ownerName}, whose {name} is {TenantValueFormatter.Format(value)}");
            }
        }

        return null;
    }

    private static Blocked? CheckEntry(
        EntityEntry entry,
        PropertyEntry property,
        TenantPropertyMetadata tenant,
        object? currentTenant,
        bool host,
        TenantGuardOptions options)
    {
        // The current tenant, or under NullTenantIsHost with none, the host, whose rows have a null tenant.
        bool Owns(object? value) => currentTenant is not null ? tenant.Matches(value, currentTenant) : host && value is null;

        var name = tenant.Property.Name;
        if (entry.State == EntityState.Added)
        {
            var value = property.CurrentValue;
            return Owns(value)
                ? null
                : Block(entry, options, currentTenant, value, $"{name} is {TenantValueFormatter.Format(value)}");
        }

        // Modified or Deleted: the stored row must be the current tenant's, and an update must not move it.
        var original = property.OriginalValue;
        if (!Owns(original))
        {
            return Block(entry, options, currentTenant, original, $"{name} is {TenantValueFormatter.Format(original)}");
        }

        var changed = property.CurrentValue;
        return entry.State == EntityState.Modified && !Owns(changed)
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
