// <copyright file="ProbeRun.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// One run of the probe: read the model through a context for the first tenant, seed a row per
// tenant for every tenant-scoped entity type, then run every check for every ordered pair of tenants,
// and NoTenantIsolation once per entity type when there is a factory for a context with no tenant.
internal sealed class ProbeRun<TContext>(
    Func<string, TContext> createContext,
    IReadOnlyList<string> tenants,
    IReadOnlyDictionary<Type, Func<string, TContext, object>> seeders,
    Func<TContext>? createContextWithoutTenant)
    where TContext : DbContext
{
    // Where the one row of an entity type that is not tenant-scoped is kept.
    private const string SharedRow = "";

    private readonly Dictionary<IEntityType, Dictionary<string, SeededRow>> _rows = [];

    private TenantGuardEntities _entities = null!;

    private SeedPlan _plan = null!;

    private HashSet<IEntityType> _perTenant = [];

    public async Task<ProbeReport> RunAsync(CancellationToken cancellationToken)
    {
        // A model that breaks a TenantGuard rule under Enforcement.Throw throws here, before anything is seeded.
        IModel model;
        TenantGuardOptions options;
        bool guarded;
        bool relational;
        await using (var context = createContext(tenants[0]))
        {
            model = context.Model;
            options = OptionsOf(context, out guarded);
            relational = IsRelational(context);
        }

        if (seeders.Keys.FirstOrDefault(type => model.FindEntityType(type) is null) is { } unknown)
        {
            throw new InvalidOperationException($"Seed<{unknown.Name}> names a type that is not an entity type of {typeof(TContext).Name}.");
        }

        _entities = TenantGuardEntities.For(model, options);
        if (_entities.CrossTenant != CrossTenantModel.No)
        {
            throw new InvalidOperationException($"{typeof(TContext).Name} is declared cross-tenant, so there is no tenant isolation to probe.");
        }

        // A type declared Unfiltered shares its rows across tenants on purpose, so it isn't probed.
        var scoped = model.GetEntityTypes()
            .Where(type => !type.IsOwned() && !type.IsAbstract() && _entities.TryGet(type, out var tenant) && !tenant.Unfiltered)
            .OrderBy(type => type.DisplayName(), StringComparer.Ordinal)
            .ToList();
        if (scoped.Count == 0)
        {
            return new ProbeReport(
                [new ProbeRow("(model)", "Discovery", ProbeOutcome.Skipped, "No entity type is tenant-scoped; check the tenant properties TenantGuard is configured with.")]);
        }

        var cannotProbe = scoped.ToDictionary(type => type, WhyCannotProbe);
        var probed = scoped.Where(type => cannotProbe[type] is null).ToList();
        CheckTenants(probed);

        var factory = new RowFactory<TContext>(seeders, _entities, new ValueGenerator());
        _plan = SeedPlan.Create(probed, factory.WhyCannotCreate, factory.TenantProperty);
        _perTenant = _plan.Order.Where(type => _entities.TryGet(type, out _) || factory.TenantKeys(type).Count > 0).ToHashSet();
        var notSeeded = await SeedAsync(factory, cancellationToken);

        var checks = new IsolationChecks<TContext>(createContext, factory, PrincipalsOf, relational, guarded);
        var targets = probed.Where(type => !notSeeded.ContainsKey(type)).Select(type => new ProbeTarget(type, EntityQueries.For(type), _rows[type])).ToList();
        var results = new Dictionary<(IEntityType, string), List<CheckResult>>();
        var navigations = scoped.ToDictionary(type => type, type => NavigationCase.For(type, _entities));
        var seeded = targets.ToDictionary(target => target.Type, target => target.Rows);
        var navigationChecks = new NavigationChecks<TContext>(
            createContext,
            factory,
            _entities,
            tenants,
            PrincipalsOf,
            type => seeded.GetValueOrDefault(type),
            type => cannotProbe.GetValueOrDefault(type) ?? notSeeded.GetValueOrDefault(type) ?? "it is not tenant-scoped");

        // Every read check runs before any write check, so a leaking write cannot change what a read sees.
        // NavigationLeak reads too, and deletes the links it seeds, so it runs between them.
        await RunChecksAsync(targets, ProbeChecks.Reads, checks, results, cancellationToken);
        if (createContextWithoutTenant is not null)
        {
            await RunWithoutTenantAsync(createContextWithoutTenant, targets, results, cancellationToken);
        }

        foreach (var target in targets)
        {
            foreach (var navigation in navigations[target.Type])
            {
                var pairs = results[(target.Type, navigation.Check)] = [];
                foreach (var (viewer, owner) in Pairs())
                {
                    pairs.Add(await navigationChecks.RunAsync(navigation, viewer, owner, cancellationToken));
                }
            }
        }

        await RunChecksAsync(targets, ProbeChecks.Writes, checks, results, cancellationToken);

        var rows = new List<ProbeRow>();
        foreach (var type in scoped)
        {
            var skipped = cannotProbe[type] ?? (notSeeded.TryGetValue(type, out var reason) ? $"Needs a seeder: {reason}." : null);
            var noTenant = createContextWithoutTenant is null ? [] : new[] { ProbeChecks.NoTenantIsolation };
            rows.AddRange(ProbeChecks.All.Concat(navigations[type].Select(navigation => navigation.Check)).Concat(noTenant).Select(check => skipped is null
                ? Combine(type.DisplayName(), check, results[(type, check)])
                : new ProbeRow(type.DisplayName(), check, ProbeOutcome.Skipped, skipped)));
        }

        return new ProbeReport(rows);
    }

    private static TenantGuardOptions OptionsOf(DbContext context, out bool guarded)
    {
        var extension = context.GetService<IDbContextOptions>().FindExtension<TenantGuardOptionsExtension>();
        guarded = extension is not null;
        return extension?.Options ?? TenantGuardOptions.Default;
    }

    private static string? WhyCannotProbe(IEntityType type)
    {
        if (type.FindPrimaryKey() is null)
        {
            return "It is keyless, so the probe cannot write or find its rows.";
        }

        return type.HasSharedClrType
            ? $"It shares the CLR type {type.ClrType.Name} with other entity types, so Set<{type.ClrType.Name}>() cannot reach it."
            : null;
    }

    // One row for a check across every pair of tenants: failed when any pair failed.
    private static ProbeRow Combine(string entity, string check, List<CheckResult> pairs)
    {
        var failures = pairs.Where(pair => pair.Outcome == ProbeOutcome.Failed).Select(pair => pair.Detail).Distinct().ToList();
        if (failures.Count > 0)
        {
            return new ProbeRow(entity, check, ProbeOutcome.Failed, string.Join(" ", failures));
        }

        if (pairs.FirstOrDefault(pair => pair.Outcome == ProbeOutcome.Skipped) is { Outcome: ProbeOutcome.Skipped } skipped)
        {
            return new ProbeRow(entity, check, ProbeOutcome.Skipped, skipped.Detail);
        }

        var notes = pairs.Select(pair => pair.Detail).Where(detail => detail.Length > 0).Distinct();
        return new ProbeRow(entity, check, ProbeOutcome.Passed, string.Join(" ", notes));
    }

    // Relational providers have transactions and ExecuteDelete. The probe has no reference to EF Core's
    // relational package, so it looks for the relational options extension by name, as IsRelational() does by type.
    private static bool IsRelational(DbContext context)
        => context.GetService<IDbContextOptions>().Extensions.Any(extension => DerivesFrom(extension.GetType(), "Microsoft.EntityFrameworkCore.Infrastructure.RelationalOptionsExtension"));

    private static bool DerivesFrom(Type? type, string fullName)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }

    private static string Message(Exception exception)
        => (exception is DbUpdateException ? exception.GetBaseException().Message : exception.Message).ReplaceLineEndings(" ");

    private async Task RunChecksAsync(
        IReadOnlyList<ProbeTarget> targets,
        IReadOnlyList<string> batch,
        IsolationChecks<TContext> checks,
        Dictionary<(IEntityType Type, string Check), List<CheckResult>> results,
        CancellationToken cancellationToken)
    {
        foreach (var target in targets)
        {
            foreach (var check in batch)
            {
                var pairs = results[(target.Type, check)] = [];
                foreach (var (viewer, owner) in Pairs())
                {
                    pairs.Add(await checks.RunAsync(check, target, viewer, owner, cancellationToken));
                }
            }
        }
    }

    // NoTenantIsolation for every target. Under CrossTenantWhenNoTenant, a no-tenant context whose model
    // is the cross-tenant one reads every tenant's rows on purpose, so each row is skipped.
    private async Task RunWithoutTenantAsync(
        Func<TContext> createWithoutTenant,
        IReadOnlyList<ProbeTarget> targets,
        Dictionary<(IEntityType Type, string Check), List<CheckResult>> results,
        CancellationToken cancellationToken)
    {
        CrossTenantModel crossTenant;
        await using (var context = createWithoutTenant())
        {
            crossTenant = TenantGuardEntities.For(context.Model, OptionsOf(context, out _)).CrossTenant;
        }

        var checks = new NoTenantChecks<TContext>(createWithoutTenant, createContext, tenants);
        foreach (var target in targets)
        {
            var result = crossTenant switch
            {
                CrossTenantModel.No => await checks.RunAsync(target, cancellationToken),
                CrossTenantModel.NoTenant => CheckResult.Skipped("The context with no tenant is declared cross-tenant by CrossTenantWhenNoTenant."),
                _ => CheckResult.Skipped("The context with no tenant is declared cross-tenant by CrossTenant."),
            };
            results[(target.Type, ProbeChecks.NoTenantIsolation)] = [result];
        }
    }

    // Every tenant has to fit every tenant property, or seeding would fail half way.
    private void CheckTenants(IEnumerable<IEntityType> probed)
    {
        foreach (var type in probed)
        {
            if (_entities.TryGet(type, out var tenant))
            {
                foreach (var name in tenants)
                {
                    TenantValues.Convert(name, tenant.Property);
                }
            }
        }
    }

    // Seeds in plan order, saving each entity type once per tenant through that tenant's context: the
    // write guard checks seeding too. Returns why each type that could not be seeded was not.
    private async Task<Dictionary<IEntityType, string>> SeedAsync(RowFactory<TContext> factory, CancellationToken cancellationToken)
    {
        var notSeeded = new Dictionary<IEntityType, string>(_plan.CannotSeed);
        var contexts = tenants.ToDictionary(tenant => tenant, createContext);
        try
        {
            foreach (var type in _plan.Order)
            {
                if (_plan.PrincipalsOf(type).FirstOrDefault(principal => notSeeded.ContainsKey(principal.EntityType)) is { } blocked)
                {
                    notSeeded[type] = SeedPlan.Blocked(blocked, factory.TenantProperty(type), "could not be seeded", notSeeded[blocked.EntityType]);
                    continue;
                }

                // A tenant-scoped type, or a principal of a tenant property, as a table of tenants: a row per tenant.
                var scoped = _perTenant.Contains(type);
                var rows = _rows[type] = [];
                foreach (var tenant in scoped ? tenants : [tenants[0]])
                {
                    var context = contexts[tenant];
                    try
                    {
                        var entry = factory.Add(context, type, tenant, PrincipalsOf(type, tenant));
                        await context.SaveChangesAsync(cancellationToken);
                        rows[scoped ? tenant : SharedRow] = SeededRow.From(entry);
                    }
                    catch (TenantIsolationViolationException exception)
                    {
                        // No violations: the context's model was built for another tenant (AllowCapturedValues).
                        throw new InvalidOperationException(
                            exception.Violations.Count == 0
                                ? $"The write guard blocked seeding {type.DisplayName()} as {tenant}, because the context's model was built for another tenant. Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own."
                                : $"The write guard blocked seeding {type.DisplayName()} as {tenant}. Does the context factory make {tenant} the context's current tenant?",
                            exception);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        foreach (var added in context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Added).ToList())
                        {
                            added.State = EntityState.Detached;
                        }

                        notSeeded[type] = $"seeding it as {tenant} failed: {Message(exception)}";
                        _rows.Remove(type);
                        break;
                    }
                }
            }
        }
        finally
        {
            foreach (var context in contexts.Values)
            {
                await context.DisposeAsync();
            }
        }

        return notSeeded;
    }

    // The principal rows a new row of the type points at: the tenant's own, or the shared one.
    private IEnumerable<(IForeignKey ForeignKey, SeededRow Principal)> PrincipalsOf(IEntityType type, string tenant)
        => _plan.PrincipalsOf(type).Select(principal =>
            (principal.ForeignKey, _rows[principal.EntityType][_perTenant.Contains(principal.EntityType) ? tenant : SharedRow]));

    private IEnumerable<(string Viewer, string Owner)> Pairs()
        => from viewer in tenants from owner in tenants where viewer != owner select (viewer, owner);
}
