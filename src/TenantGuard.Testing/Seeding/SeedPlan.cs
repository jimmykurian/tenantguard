// <copyright file="SeedPlan.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// The entity types to seed and their order: the probed types and, through every required
// relationship, the principals they need, each after its principals. A relationship whose foreign key
// includes the tenant property needs its principal too, required or not: the tenant value points at
// it. A type in a cycle of required relationships, or one whose principal cannot be seeded, gets the
// reason instead of a place.
internal sealed class SeedPlan
{
    private readonly Dictionary<IEntityType, List<RequiredPrincipal>> _principals;

    private SeedPlan(
        Dictionary<IEntityType, List<RequiredPrincipal>> principals,
        IReadOnlyList<IEntityType> order,
        IReadOnlyDictionary<IEntityType, string> cannotSeed)
    {
        _principals = principals;
        Order = order;
        CannotSeed = cannotSeed;
    }

    // The types that can be seeded, every principal before its dependents.
    public IReadOnlyList<IEntityType> Order { get; }

    // Why each of the other types cannot be seeded.
    public IReadOnlyDictionary<IEntityType, string> CannotSeed { get; }

    // whyCannotCreate says why a type's own rows cannot be created, or returns null; tenantPropertyOf
    // gives a type's tenant property, or null.
    public static SeedPlan Create(
        IEnumerable<IEntityType> targets, Func<IEntityType, string?> whyCannotCreate, Func<IEntityType, IProperty?> tenantPropertyOf)
    {
        var principals = new Dictionary<IEntityType, List<RequiredPrincipal>>();
        var cannotSeed = new Dictionary<IEntityType, string>();
        var pending = new Queue<IEntityType>(targets);
        while (pending.TryDequeue(out var type))
        {
            if (principals.ContainsKey(type))
            {
                continue;
            }

            var required = new List<RequiredPrincipal>();
            principals[type] = required;
            if (whyCannotCreate(type) is { } reason)
            {
                cannotSeed[type] = reason;
            }

            var tenantProperty = tenantPropertyOf(type);
            foreach (var foreignKey in type.GetForeignKeys().Where(foreignKey =>
                !foreignKey.IsOwnership && (foreignKey.IsRequired || foreignKey.Properties.Contains(tenantProperty))))
            {
                if (foreignKey.PrincipalEntityType.GetDerivedTypesInclusive().FirstOrDefault(candidate => !candidate.IsAbstract()) is not { } principal)
                {
                    cannotSeed.TryAdd(type, $"its required principal {foreignKey.PrincipalEntityType.DisplayName()} has no concrete type");
                    continue;
                }

                required.Add(new RequiredPrincipal(foreignKey, principal));
                pending.Enqueue(principal);
            }
        }

        // Take every type whose principals are all placed, until none is left or the rest wait on each other.
        var order = new List<IEntityType>();
        var waiting = principals.Keys.ToHashSet();
        while (waiting.Where(type => principals[type].All(principal => !waiting.Contains(principal.EntityType))).OrderBy(type => type.Name, StringComparer.Ordinal).ToList() is { Count: > 0 } ready)
        {
            foreach (var type in ready)
            {
                waiting.Remove(type);
                order.Add(type);
            }
        }

        foreach (var type in waiting)
        {
            cannotSeed.TryAdd(type, "its required relationships form a cycle");
        }

        foreach (var type in order.Where(type => !cannotSeed.ContainsKey(type)))
        {
            if (principals[type].FirstOrDefault(principal => cannotSeed.ContainsKey(principal.EntityType)) is { } blocked)
            {
                cannotSeed[type] = Blocked(blocked, tenantPropertyOf(type), "cannot be seeded", cannotSeed[blocked.EntityType]);
            }
        }

        return new SeedPlan(principals, [.. order.Where(type => !cannotSeed.ContainsKey(type))], cannotSeed);
    }

    public IReadOnlyList<RequiredPrincipal> PrincipalsOf(IEntityType type) => _principals[type];

    // Why a type can't be seeded when its principal can't: through the tenant property, as for a table of
    // tenants, that says so and why.
    internal static string Blocked(RequiredPrincipal principal, IProperty? tenantProperty, string cannot, string why)
        => tenantProperty is not null && principal.ForeignKey.Properties.Contains(tenantProperty)
            ? $"its tenant property {tenantProperty.Name} is a foreign key to {principal.EntityType.DisplayName()}, which {cannot}: {why}"
            : $"its required principal {principal.EntityType.DisplayName()} {cannot}";

    // A required relationship of a seeded type, and the concrete type seeded as its principal.
    internal sealed record RequiredPrincipal(IForeignKey ForeignKey, IEntityType EntityType);
}
