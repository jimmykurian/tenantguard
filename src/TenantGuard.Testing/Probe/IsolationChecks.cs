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
    private static EntityEntry AttachCopy(DbContext context, SeededRow row)
    {
        var entry = context.Attach(row.Values.ToObject());
        entry.CurrentValues.SetValues(row.Values);
        entry.OriginalValues.SetValues(row.Values);
        entry.State = EntityState.Unchanged;
        return entry;
    }

    private static string OneLine(string message) => message.ReplaceLineEndings(" ");

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
            entry.Property(property).CurrentValue = factory.NextValue(property);
        }

        entry.State = EntityState.Modified;
        return await ExpectBlockedAsync(context, viewer, $"updated {owner}'s row {theirs.FormatKey()}", cancellationToken);
    }

    private async Task<CheckResult> InsertAsync(ProbeTarget target, string viewer, string owner, CancellationToken cancellationToken)
    {
        await using var context = createContext(viewer);
        await using var transaction = await BeginAsync(context, cancellationToken);
        factory.Add(context, target.Type, owner, principalsOf(target.Type, owner));
        return await ExpectBlockedAsync(context, viewer, $"inserted a {target.Name} row stamped {owner}", cancellationToken);
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

    // The write guard should throw before the write reaches the database.
    private async Task<CheckResult> ExpectBlockedAsync(DbContext context, string viewer, string write, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (TenantIsolationViolationException)
        {
            return CheckResult.Passed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CheckResult.Failed(
                $"As {viewer}, SaveChanges threw {exception.GetType().Name} rather than TenantIsolationViolationException: {OneLine(exception.GetBaseException().Message)}");
        }

        return CheckResult.Failed(guarded
            ? $"As {viewer}, SaveChanges {write}."
            : $"As {viewer}, SaveChanges {write}; the context does not use UseTenantGuard.");
    }
}
