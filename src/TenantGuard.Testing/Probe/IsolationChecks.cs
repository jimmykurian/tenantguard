// <copyright file="IsolationChecks.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace TenantGuard.Testing;

// The probe's six checks for one entity type, as the viewer against the owner's row, each with a fresh
// context for the viewer. The four that write run in a transaction that is rolled back when the provider
// has transactions, so a write that leaks does not change what the next check sees.
internal sealed class IsolationChecks<TContext>(
    Func<string, TContext> createContext,
    RowFactory<TContext> factory,
    Func<IEntityType, string, IEnumerable<(IForeignKey ForeignKey, SeededRow Principal)>> principalsOf,
    bool relational,
    bool guarded)
    where TContext : DbContext
{
    public Task<CheckResult> RunAsync(string check, ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
        => check switch
        {
            ProbeChecks.ReadIsolation => ReadAsync(target, viewer, owner, cancellationToken),
            ProbeChecks.FindIsolation => FindAsync(target, viewer, owner, cancellationToken),
            ProbeChecks.BulkIsolation => BulkDeleteAsync(target, viewer, owner, cancellationToken),
            ProbeChecks.UpdateGuard => UpdateAsync(target, viewer, owner, cancellationToken),
            ProbeChecks.InsertGuard => InsertAsync(target, viewer, owner, cancellationToken),
            ProbeChecks.DeleteGuard => DeleteAsync(target, viewer, owner, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(check), check, "Unknown check."),
        };

    // A copy of the owner's row, attached to the viewer's context as if it had been read there.
    internal static EntityEntry AttachCopy(DbContext context, SeededRow row)
    {
        var entry = context.Attach(row.Values.ToObject());

        // By name: with a model per tenant, the row's values belong to another model.
        foreach (var property in entry.Metadata.GetProperties())
        {
            var value = row.Values[property.Name];
            var current = entry.Property(property);
            current.CurrentValue = value;
            current.OriginalValue = value;
        }

        entry.State = EntityState.Unchanged;
        return entry;
    }

    private static string OneLine(string message) => message.ReplaceLineEndings(" ");

    private static string Article(string name)
        => name.Length > 0 && "AEIOUaeiou".Contains(name[0], StringComparison.Ordinal) ? "an" : "a";

    private static bool IsDatabaseError(Exception exception)
        => exception is DbUpdateException or DbException || exception.GetBaseException() is DbException;

    private async Task<CheckResult> ReadAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        var entries = (await target.Queries.ListAsync(context, cancellationToken)).Select(row => context.Entry(row)).ToList();
        var theirs = target.Rows[owner];
        if (entries.Any(theirs.HasKey))
        {
            return CheckResult.Failed($"As {viewer}, Set<{target.Name}>() returned {owner}'s row {theirs.FormatKey()}.");
        }

        // A tenant that cannot see its own row would pass this check without proving anything.
        var own = target.Rows[viewer];
        return entries.Any(own.HasKey)
            ? CheckResult.Passed
            : CheckResult.Skipped($"As {viewer}, Set<{target.Name}>() did not return {viewer}'s own row {own.FormatKey()}, so the check proves nothing. Does every context the factory creates use the same database?");
    }

    private async Task<CheckResult> FindAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        var theirs = target.Rows[owner];
        return await target.Queries.FindAsync(context, theirs.KeyValues, cancellationToken) is null
            ? CheckResult.Passed
            : CheckResult.Failed($"As {viewer}, FindAsync{theirs.FormatKey()} returned {owner}'s row.");
    }

    // ExecuteDelete aimed at the owner's key, which deletes nothing when the query filter holds.
    private async Task<CheckResult> BulkDeleteAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        if (!relational)
        {
            return CheckResult.Skipped("The provider does not support ExecuteDelete.");
        }

        await using var context = createContext(viewer);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var theirs = target.Rows[owner];
        try
        {
            return await target.Queries.ExecuteDeleteAsync(context, theirs.KeyValues, cancellationToken) == 0
                ? CheckResult.Passed
                : CheckResult.Failed($"As {viewer}, ExecuteDelete removed {owner}'s row {theirs.FormatKey()}.");
        }
        catch (DbException exception)
        {
            return CheckResult.Failed($"As {viewer}, ExecuteDelete reached {owner}'s row {theirs.FormatKey()}: {OneLine(exception.Message)}");
        }
        catch (InvalidOperationException exception)
        {
            return CheckResult.Skipped($"EF Core cannot run ExecuteDelete on {target.Name}: {OneLine(exception.Message)}");
        }
    }

    private async Task<CheckResult> UpdateAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        await using var transaction = await BeginAsync(context, cancellationToken);
        var theirs = target.Rows[owner];
        var entry = AttachCopy(context, theirs);
        if (factory.PropertyToChange(target.Type) is { } property)
        {
            entry.Property(property.Name).CurrentValue = factory.NextValue(property);
        }

        entry.State = EntityState.Modified;
        return await ExpectBlockedAsync(context, viewer, $"updated {owner}'s row {theirs.FormatKey()}", cancellationToken);
    }

    private async Task<CheckResult> InsertAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        await using var transaction = await BeginAsync(context, cancellationToken);
        var entry = factory.Add(context, target.Type, owner, principalsOf(target.Type, owner));

        // The app's SaveChanges can stamp an added row with the current tenant, as Finbuckle's
        // TenantMismatchMode.Overwrite does: then the row is the viewer's own, and nothing crosses.
        var tenantProperty = factory.TenantProperty(target.Type);
        bool StampedAsViewer()
            => tenantProperty is not null && Equals(entry.Property(tenantProperty.Name).CurrentValue, TenantValues.Convert(viewer, tenantProperty));

        return await ExpectBlockedAsync(
            context,
            viewer,
            $"inserted {Article(target.Name)} {target.Name} row stamped {owner}",
            cancellationToken,
            StampedAsViewer);
    }

    private async Task<CheckResult> DeleteAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        await using var transaction = await BeginAsync(context, cancellationToken);
        var theirs = target.Rows[owner];
        AttachCopy(context, theirs).State = EntityState.Deleted;
        return await ExpectBlockedAsync(context, viewer, $"deleted {owner}'s row {theirs.FormatKey()}", cancellationToken);
    }

    private async Task<IDbContextTransaction?> BeginAsync(DbContext context, CancellationToken cancellationToken)
        => relational ? await context.Database.BeginTransactionAsync(cancellationToken) : null;

    // The write guard should throw before the write reaches the database. The app's own SaveChanges
    // can block it first, as Finbuckle's MultiTenantDbContext does by default: any exception other
    // than a database error passes, when every change the check made is still pending, so EF Core
    // saved none of them.
    // stampedAsViewer, for an insert, says whether the app's SaveChanges stamped the new row with the
    // viewer's tenant, which passes whether the database then stored the row or refused it.
    private async Task<CheckResult> ExpectBlockedAsync(
        DbContext context,
        string viewer,
        string write,
        CancellationToken cancellationToken,
        Func<bool>? stampedAsViewer = null)
    {
        var pending = context.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => (Entry: entry, entry.State))
            .ToList();
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (TenantIsolationViolationException)
        {
            return CheckResult.Passed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            && !IsDatabaseError(exception)
            && pending.All(change => change.Entry.State == change.State))
        {
            return CheckResult.PassedWith(
                $"As {viewer}, the app's SaveChanges threw {exception.GetType().Name} before the database, so nothing was saved: {OneLine(exception.GetBaseException().Message)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException && IsDatabaseError(exception) && stampedAsViewer?.Invoke() == true)
        {
            return CheckResult.PassedWith(
                $"As {viewer}, the app's SaveChanges stamped the new row {viewer}, and the database refused it: {OneLine(exception.GetBaseException().Message)}");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CheckResult.Failed(
                $"As {viewer}, SaveChanges threw {exception.GetType().Name} rather than TenantIsolationViolationException: {OneLine(exception.GetBaseException().Message)}");
        }

        if (stampedAsViewer?.Invoke() == true)
        {
            return CheckResult.PassedWith($"As {viewer}, the app's SaveChanges stamped the new row {viewer}, so it was saved as {viewer}'s own.");
        }

        return CheckResult.Failed(guarded
            ? $"As {viewer}, SaveChanges {write}."
            : $"As {viewer}, SaveChanges {write}; the context does not use UseTenantGuard.");
    }
}
