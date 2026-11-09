// <copyright file="NavigationChecks.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// The NavigationLeak check for one navigation, as the viewer against the owner: link the viewer's row to
// the owner's, load the viewer's row with Include of the navigation, and fail when it holds a row of
// another tenant. The link is saved through its own tenant's context and deleted afterwards.
internal sealed class NavigationChecks<TContext>(
    Func<string, TContext> createContext,
    RowFactory<TContext> factory,
    TenantGuardEntities entities,
    IReadOnlyList<string> tenants,
    Func<IEntityType, string, IEnumerable<(IForeignKey ForeignKey, SeededRow Principal)>> principalsOf,
    Func<IEntityType, IReadOnlyDictionary<string, SeededRow>?> rowsOf,
    Func<IEntityType, string> whyNoRows)
    where TContext : DbContext
{
    public async Task<CheckResult> RunAsync(NavigationCase navigation, string viewer, string owner, CancellationToken cancellationToken)
    {
        if (navigation.TenantInForeignKey)
        {
            return CheckResult.Passed;
        }

        foreach (var needed in new[] { navigation.Type, navigation.Target, navigation.Dependent }.OfType<IEntityType>().Distinct())
        {
            if (rowsOf(needed) is null)
            {
                return CheckResult.Skipped($"The link needs a {needed.DisplayName()} row, which the probe cannot seed: {whyNoRows(needed)}.");
            }
        }

        return navigation.Kind switch
        {
            NavigationKind.ToPrincipal => await ThroughDependentAsync(
                navigation, viewer, owner, linkTenant: viewer, principal: rowsOf(navigation.Target)![owner], cancellationToken),
            NavigationKind.ToDependents => await ThroughDependentAsync(
                navigation, viewer, owner, linkTenant: owner, principal: rowsOf(navigation.Type)![viewer], cancellationToken),
            _ => await ThroughJoinAsync(navigation, viewer, owner, cancellationToken),
        };
    }

    private static string OneLine(string message) => message.ReplaceLineEndings(" ");

    private static object? TenantOf(EntityEntry entry, IProperty property) => entry.Property(property.Name).CurrentValue;

    private static async Task<string?> TrySaveAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException)
        {
            foreach (var added in context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
            {
                added.State = EntityState.Detached;
            }

            return OneLine(exception.GetBaseException().Message);
        }
    }

    // A new dependent stamped linkTenant whose foreign key points at the other tenant's principal.
    private async Task<CheckResult> ThroughDependentAsync(
        NavigationCase navigation, string viewer, string owner, string linkTenant, SeededRow principal, CancellationToken cancellationToken)
    {
        var foreignKey = navigation.ForeignKey!;
        var dependent = navigation.Dependent!;
        await using var linkContext = createContext(linkTenant);
        var entry = factory.Add(
            linkContext,
            dependent,
            linkTenant,
            principalsOf(dependent, linkTenant).Where(required => required.ForeignKey != foreignKey).Append((foreignKey, principal)));

        // A seeder may set the foreign key itself; the link has to point across tenants all the same.
        for (var index = 0; index < foreignKey.Properties.Count; index++)
        {
            entry.Property(foreignKey.Properties[index].Name).CurrentValue = principal.Values[foreignKey.PrincipalKey.Properties[index].Name];
        }

        if (await TrySaveAsync(linkContext, cancellationToken) is { } error)
        {
            return CheckResult.Skipped($"Saving the link for {navigation.Name} as {linkTenant} failed: {error}");
        }

        var link = SeededRow.From(entry);
        try
        {
            // Toward the principal, the viewer's row is the link itself; toward dependents, it's the viewer's seeded row.
            // The owner's row the navigation could leak is the principal, or toward dependents the link.
            var viewersRow = navigation.Kind == NavigationKind.ToPrincipal ? link : rowsOf(navigation.Type)![viewer];
            var (ownersType, ownersRow) = navigation.Kind == NavigationKind.ToPrincipal ? (navigation.Target, principal) : (dependent, link);
            return await LoadAsync(navigation, viewer, viewersRow, owner, ownersType, ownersRow, cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Deleted;
            await linkContext.SaveChangesAsync(cancellationToken);
        }
    }

    // A join row between the viewer's row and the owner's target row, saved through the viewer's context.
    private async Task<CheckResult> ThroughJoinAsync(NavigationCase navigation, string viewer, string owner, CancellationToken cancellationToken)
    {
        var skip = (ISkipNavigation)navigation.Navigation;
        var viewersRow = rowsOf(navigation.Type)![viewer];
        await using var linkContext = createContext(viewer);
        var mine = IsolationChecks<TContext>.AttachCopy(linkContext, viewersRow);
        var theirs = IsolationChecks<TContext>.AttachCopy(linkContext, rowsOf(navigation.Target)![owner]);
        skip.GetCollectionAccessor()!.Add(mine.Entity, theirs.Entity, forMaterialization: false);
        linkContext.ChangeTracker.DetectChanges();
        var joins = linkContext.ChangeTracker.Entries().Where(entry => entry.Metadata.Name == skip.JoinEntityType.Name && entry.State == EntityState.Added).ToList();

        if (await TrySaveAsync(linkContext, cancellationToken) is { } error)
        {
            return CheckResult.Skipped($"Saving the {skip.JoinEntityType.DisplayName()} link for {navigation.Name} as {viewer} failed: {error}");
        }

        try
        {
            return await LoadAsync(navigation, viewer, viewersRow, owner, navigation.Target, rowsOf(navigation.Target)![owner], cancellationToken);
        }
        finally
        {
            foreach (var join in joins)
            {
                join.State = EntityState.Deleted;
            }

            await linkContext.SaveChangesAsync(cancellationToken);
        }
    }

    // As the viewer, loads the viewer's row with Include of the navigation; any row of another tenant in it is a leak.
    // A pass proves nothing when another filter hides the viewer's row or the owner's from their own tenant.
    private async Task<CheckResult> LoadAsync(
        NavigationCase navigation,
        string viewer,
        SeededRow viewersRow,
        string owner,
        IEntityType ownersType,
        SeededRow ownersRow,
        CancellationToken cancellationToken)
        => await LeakAsync(navigation, viewer, viewersRow, cancellationToken)
            ?? await IsolationChecks<TContext>.HiddenFromItsTenantAsync(createContext, navigation.Type, viewersRow, viewer, cancellationToken)
            ?? await IsolationChecks<TContext>.HiddenFromItsTenantAsync(createContext, ownersType, ownersRow, owner, cancellationToken)
            ?? CheckResult.Passed;

    // Failed when the navigation loaded a row of another tenant, or null.
    private async Task<CheckResult?> LeakAsync(NavigationCase navigation, string viewer, SeededRow viewersRow, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        var row = await EntityQueries.For(navigation.Type).IncludeAsync(context, viewersRow.KeyValues, navigation.Name, cancellationToken);

        // A required navigation to a filtered principal hides the whole row, which leaks nothing.
        if (row is null)
        {
            return null;
        }

        var value = context.Entry(row).Navigation(navigation.Name).CurrentValue;
        var loaded = value is null ? [] : navigation.Navigation.IsCollection ? ((IEnumerable)value).Cast<object>() : [value];
        foreach (var item in loaded)
        {
            var itemEntry = context.Entry(item);
            if (entities.TryGet(itemEntry.Metadata.Name, out var tenant)
                && TenantOf(itemEntry, tenant.Property) is var itemTenant
                && !tenant.Matches(itemTenant, TenantValues.Convert(viewer, tenant.Property)))
            {
                return CheckResult.Failed(
                    $"As {viewer}, Include({navigation.Name}) on {viewer}'s {navigation.Type.DisplayName()} {viewersRow.FormatKey()} "
                    + $"loaded {Name(itemTenant, tenant)}'s {itemEntry.Metadata.DisplayName()} {SeededRow.From(itemEntry).FormatKey()}.");
            }
        }

        return null;
    }

    // The probe's name for the tenant, or the value as TenantGuard formats it when no probe tenant has it.
    private string Name(object? value, TenantPropertyMetadata tenant)
        => tenants.FirstOrDefault(name => tenant.Matches(value, TenantValues.Convert(name, tenant.Property))) ?? TenantValueFormatter.Format(value);
}
