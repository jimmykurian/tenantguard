// <copyright file="NoTenantChecks.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Testing;

// NoTenantIsolation for one entity type: as a fresh context with no tenant, Set<E>() returns none of
// the seeded tenants' rows, and FindAsync with each tenant's key returns null. Only the rows their own
// tenant can read, or find, are checked; a query that throws returns nothing, so it passes, naming the
// exception. An exception from the factory itself isn't caught.
internal sealed class NoTenantChecks<TContext>(
    Func<TContext> createContext,
    Func<string, TContext> createTenantContext,
    IReadOnlyList<string> tenants)
    where TContext : DbContext
{
    public async Task<CheckResult> RunAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        var (readable, findable) = await VisibleToTheirTenantsAsync(target, cancellationToken);
        if (readable.Count == 0 && findable.Count == 0)
        {
            return CheckResult.Skipped($"No tenant could read or find its own {target.Name} row, so the check proves nothing.");
        }

        var failures = new List<string>();
        var notes = new List<string>();
        await using var context = createContext();
        if (readable.Count > 0)
        {
            try
            {
                var entries = (await target.Queries.ListAsync(context, cancellationToken)).Select(row => context.Entry(row)).ToList();
                var leaked = readable.Where(tenant => entries.Any(target.Rows[tenant].HasKey)).ToList();
                if (leaked.Count > 0)
                {
                    failures.Add($"As no tenant, Set<{target.Name}>() returned {List(leaked.Select(tenant => $"{tenant}'s row {target.Rows[tenant].FormatKey()}"))}.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notes.Add($"As no tenant, Set<{target.Name}>() threw {exception.GetType().Name}, so it returned no rows: {OneLine(exception)}");
            }
        }

        // A row the list tracked would answer FindAsync without a query.
        context.ChangeTracker.Clear();
        foreach (var tenant in findable)
        {
            var row = target.Rows[tenant];
            try
            {
                if (await target.Queries.FindAsync(context, row.KeyValues, cancellationToken) is not null)
                {
                    failures.Add($"As no tenant, FindAsync{row.FormatKey()} returned {tenant}'s row.");
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notes.Add($"As no tenant, FindAsync threw {exception.GetType().Name}, so it returned no row: {OneLine(exception)}");
            }
        }

        return failures.Count > 0
            ? CheckResult.Failed(string.Join(" ", failures))
            : CheckResult.PassedWith(string.Join(" ", notes.Distinct()));
    }

    private static string OneLine(Exception exception) => exception.GetBaseException().Message.ReplaceLineEndings(" ");

    // acme's row (Id: 1), globex's row (Id: 2) and initech's row (Id: 3).
    private static string List(IEnumerable<string> items)
        => items.ToList() switch
        {
            [var only] => only,
            [.. var first, var last] => $"{string.Join(", ", first)} and {last}",
            [] => string.Empty,
        };

    // The tenants whose own row their own context returns from Set<E>(), and finds with FindAsync: a row
    // another query filter, such as a soft delete, hides from its own tenant proves nothing.
    private async Task<(List<string> Readable, List<string> Findable)> VisibleToTheirTenantsAsync(ProbeTarget target, CancellationToken cancellationToken)
    {
        var readable = new List<string>();
        var findable = new List<string>();
        foreach (var tenant in tenants)
        {
            var own = target.Rows[tenant];
            await using var context = createTenantContext(tenant);
            if ((await target.Queries.ListAsync(context, cancellationToken)).Any(row => own.HasKey(context.Entry(row))))
            {
                readable.Add(tenant);
            }

            context.ChangeTracker.Clear();
            if (await target.Queries.FindAsync(context, own.KeyValues, cancellationToken) is not null)
            {
                findable.Add(tenant);
            }
        }

        return (readable, findable);
    }
}
