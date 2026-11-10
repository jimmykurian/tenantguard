// <copyright file="RowFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// Creates one row of an entity type for a tenant: with the seeder Seed<T> set, else with every
// required value the database does not generate filled, through a parameterless constructor or the
// constructor EF Core binds. Either way the tenant property gets the tenant, and a required foreign key
// left unset points at the principal row seeded for the same tenant, or at the one shared row of a
// principal that is not tenant-scoped. A key that another type's tenant property points at, as in a
// table of tenants, gets the tenant too, so that type's rows have a principal per tenant.
internal sealed class RowFactory<TContext>(
    IReadOnlyDictionary<Type, Func<string, TContext, object>> seeders,
    TenantGuardEntities entities,
    ValueGenerator generator)
    where TContext : DbContext
{
    private const BindingFlags Constructors = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    // TenantKeys per model, worked out once: a model per tenant has entity types of its own.
    private readonly Dictionary<IModel, IReadOnlyDictionary<IEntityType, IReadOnlyList<IProperty>>> _tenantKeys = [];

    // Why the default seeder cannot create a row of the type, or null when it can or Seed<T> is set.
    public string? WhyCannotCreate(IEntityType type)
    {
        if (seeders.ContainsKey(type.ClrType))
        {
            return null;
        }

        if (type.HasSharedClrType)
        {
            return $"it shares the CLR type {type.ClrType.Name} with other entity types";
        }

        if (!CanInstantiate(type))
        {
            return $"{type.ClrType.Name} has no parameterless constructor, and EF Core doesn't bind one of its constructors";
        }

        if (type.GetComplexProperties().FirstOrDefault(complex => !complex.IsNullable) is { } required)
        {
            return $"the default seeder does not fill the required complex property {required.Name}";
        }

        if (FilledProperties(type).Select(ValueGenerator.WhyCannotFill).FirstOrDefault(reason => reason is not null) is { } unfillable)
        {
            return unfillable;
        }

        var filled = FilledProperties(type).ToList();
        return WhyCannotFillOwned(type, Sample(type, filled));
    }

    // The keys of the type that a tenant value reaches as a foreign key, other than the type's own
    // tenant property: each row seeded for a tenant gets the tenant in them. Another type's tenant
    // property points at them, or a key of another type that a tenant value reaches in turn, as along
    // a shared primary key (Role.TenantId to Tenant.Id, which is the foreign key to Organization.Id).
    public IReadOnlyList<IProperty> TenantKeys(IEntityType type)
    {
        var model = type.Model;
        if (!_tenantKeys.TryGetValue(model, out var keys))
        {
            keys = _tenantKeys[model] = KeysATenantReaches(model);
        }

        return keys.GetValueOrDefault(type) ?? [];
    }

    // The type's tenant property, or null when it is not tenant-scoped.
    public IProperty? TenantProperty(IEntityType type)
        => entities.TryGet(type, out var tenant) ? tenant.Property : null;

    // Creates the row and adds it to the context; the caller saves.
    public EntityEntry Add(TContext context, IEntityType type, string tenant, IEnumerable<(IForeignKey ForeignKey, SeededRow Principal)> principals)
    {
        var seeded = seeders.TryGetValue(type.ClrType, out var seed);
        var tenantProperty = entities.TryGet(type, out var tenantMetadata) ? tenantMetadata.Property : null;
        var values = new List<(IProperty Property, object? Value, bool OnlyIfUnset)>();
        if (tenantProperty is not null)
        {
            values.Add((tenantProperty, TenantValues.Convert(tenant, tenantProperty), false));
        }

        var tenantKeys = TenantKeys(type);
        values.AddRange(tenantKeys.Select(key => (key, (object?)TenantValues.Convert(tenant, key), false)));

        foreach (var (foreignKey, principal) in principals)
        {
            for (var index = 0; index < foreignKey.Properties.Count; index++)
            {
                if (foreignKey.Properties[index] != tenantProperty && !tenantKeys.Contains(foreignKey.Properties[index]))
                {
                    values.Add((foreignKey.Properties[index], principal.Values[foreignKey.PrincipalKey.Properties[index].Name], seeded));
                }
            }
        }

        object entity;
        if (seeded)
        {
            entity = seed!(tenant, context);
        }
        else
        {
            values.AddRange(FilledProperties(type).Select(property => (property, generator.Next(property), false)));
            entity = Instantiate(type, context, property => values.FirstOrDefault(value => value.Property == property).Value);
            FillOwned(type, entity, context);
        }

        // A key EF does not generate has to be on the entity before it is tracked; shadow values come after.
        var afterTracking = new List<(IProperty Property, object? Value, bool OnlyIfUnset)>();
        foreach (var value in values)
        {
            if (!TrySetClrValue(value.Property, entity, value.Value, value.OnlyIfUnset))
            {
                afterTracking.Add(value);
            }
        }

        var entry = context.Add(entity);
        foreach (var (property, value, onlyIfUnset) in afterTracking)
        {
            var current = entry.Property(property.Name);
            if (!onlyIfUnset || IsUnset(property, current.CurrentValue))
            {
                current.CurrentValue = value;
            }
        }

        return entry;
    }

    // A required property to change for an update, or null to mark the whole row modified.
    public IProperty? PropertyToChange(IEntityType type) => FilledProperties(type).FirstOrDefault(property => !property.IsKey());

    public object? NextValue(IProperty property) => generator.Next(property);

    private static bool IsUnset(IReadOnlyProperty property, object? value) => value is null || Equals(value, property.Sentinel);

    private static bool CanInstantiate(ITypeBase type)
        => type.ClrType.GetConstructor(Constructors, Type.EmptyTypes) is not null || type.ConstructorBinding is ConstructorBinding;

    // Through the parameterless constructor, or as EF Core does, through the constructor it binds: a
    // property parameter gets valueOf's value, or the parameter type's default.
    private static object Instantiate(ITypeBase type, DbContext? context, Func<IProperty, object?> valueOf)
    {
        if (type.ClrType.GetConstructor(Constructors, Type.EmptyTypes) is not null)
        {
            return Activator.CreateInstance(type.ClrType, nonPublic: true)!;
        }

        var binding = (ConstructorBinding)type.ConstructorBinding!;
        var arguments = binding.ParameterBindings.Select(parameter => parameter switch
        {
            PropertyParameterBinding { ConsumedProperties: [IProperty property] } => Fit(valueOf(property), parameter.ParameterType),
            ContextParameterBinding => context,
            EntityTypeParameterBinding => type,
            _ => Fit(null, parameter.ParameterType),
        }).ToArray();
        return binding.Constructor.Invoke(arguments);
    }

    // The value when the parameter takes it, else the parameter type's default.
    private static object? Fit(object? value, Type parameterType)
        => value is not null && parameterType.IsInstanceOfType(value)
            ? value
            : parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) is null ? Activator.CreateInstance(parameterType) : null;

    // The required values of an owned type the default seeder fills when they are left null.
    private static IEnumerable<IProperty> OwnedProperties(IEntityType owned)
        => owned.GetProperties().Where(property => !property.IsNullable && !property.IsShadowProperty() && !property.IsKey() && property.ValueGenerated == ValueGenerated.Never);

    // The owned values passed to the constructor EF Core binds, none with a parameterless constructor.
    private static IReadOnlyList<IProperty> ConstructorProperties(IEntityType owned)
        => owned.ClrType.GetConstructor(Constructors, Type.EmptyTypes) is null && owned.ConstructorBinding is ConstructorBinding binding
            ? [.. binding.ParameterBindings.OfType<PropertyParameterBinding>().SelectMany(parameter => parameter.ConsumedProperties).OfType<IProperty>().Intersect(OwnedProperties(owned))]
            : [];

    // A new instance of the type, made as the seeder makes one, to see which values its constructor and
    // initializers set; arguments are the values generated for the constructor, any other parameter
    // gets its default. Null when the instance can't be made here: a value can't be generated, or the
    // constructor throws.
    private static object? Sample(ITypeBase type, IReadOnlyCollection<IProperty> arguments)
    {
        var sampler = new ValueGenerator();
        try
        {
            return arguments.Any(property => ValueGenerator.WhyCannotFill(property) is not null)
                ? null
                : Instantiate(type, context: null, property => arguments.Contains(property) ? sampler.Next(property) : null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    // Why the seeder can't fill an owned reference of the type, or null. instance is the owner as the
    // seeder makes it, or null when it couldn't be made: an owned value it already holds, set by the
    // owned type or by the owner, is kept, so only the values left null have to be fillable.
    private static string? WhyCannotFillOwned(IEntityType type, object? instance)
    {
        foreach (var navigation in OwnedReferences(type))
        {
            var owned = navigation.TargetEntityType;
            var existing = instance is null ? null : GetClrValue(navigation, instance);
            IEnumerable<IProperty> toFill;
            if (existing is null)
            {
                if (!navigation.ForeignKey.IsRequiredDependent)
                {
                    continue;
                }

                if (!CanInstantiate(owned))
                {
                    return $"the owned type {owned.ClrType.Name} has no parameterless constructor, and EF Core doesn't bind one of its constructors";
                }

                var arguments = ConstructorProperties(owned);
                existing = Sample(owned, arguments);
                toFill = arguments.Concat(OwnedProperties(owned).Except(arguments).Where(property => existing is null || GetClrValue(property, existing) is null));
            }
            else
            {
                toFill = OwnedProperties(owned).Where(property => GetClrValue(property, existing) is null);
            }

            if ((toFill.Select(ValueGenerator.WhyCannotFill).FirstOrDefault(reason => reason is not null) ?? WhyCannotFillOwned(owned, existing)) is { } reason)
            {
                return reason;
            }
        }

        return null;
    }

    private static bool TrySetClrValue(IPropertyBase property, object entity, object? value, bool onlyIfUnset)
    {
        if (property.IsShadowProperty())
        {
            return false;
        }

        var setter = property.PropertyInfo is { SetMethod: not null } propertyInfo ? propertyInfo : null;
        if (setter is null && property.FieldInfo is null)
        {
            return false;
        }

        if (onlyIfUnset && property is IReadOnlyProperty scalar && !IsUnset(scalar, GetClrValue(property, entity)))
        {
            return true;
        }

        if (setter is not null)
        {
            setter.SetValue(entity, value);
        }
        else
        {
            property.FieldInfo!.SetValue(entity, value);
        }

        return true;
    }

    private static object? GetClrValue(IPropertyBase property, object entity)
        => property.PropertyInfo is { GetMethod: not null } propertyInfo ? propertyInfo.GetValue(entity) : property.FieldInfo?.GetValue(entity);

    private static IEnumerable<INavigation> OwnedReferences(IEntityType type)
        => type.GetNavigations().Where(navigation => !navigation.IsCollection && navigation.ForeignKey.IsOwnership && !navigation.IsOnDependent);

    // For each entity type, the keys a tenant value reaches, other than its own tenant property: worked
    // out from the tenant properties along foreign keys until nothing changes, so each relationship is
    // followed once per key it adds, however the relationships branch or cycle.
    private Dictionary<IEntityType, IReadOnlyList<IProperty>> KeysATenantReaches(IModel model)
    {
        var types = model.GetEntityTypes().ToList();
        var reached = types.ToDictionary(type => type, type => new HashSet<IProperty>(TenantProperty(type) is { } tenant ? [tenant] : []));
        var foreignKeys = types.SelectMany(type => type.GetDeclaredForeignKeys()).Where(foreignKey => !foreignKey.IsOwnership).ToList();
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var foreignKey in foreignKeys)
            {
                for (var index = 0; index < foreignKey.Properties.Count; index++)
                {
                    changed |= reached[foreignKey.DeclaringEntityType].Contains(foreignKey.Properties[index])
                        && reached[foreignKey.PrincipalEntityType].Add(foreignKey.PrincipalKey.Properties[index]);
                }
            }
        }

        // A type's keys include those reached through a relationship to one of its base types.
        return types.ToDictionary(
            type => type,
            type => (IReadOnlyList<IProperty>)[.. type.GetAllBaseTypesInclusive().SelectMany(baseType => reached[baseType]).Distinct().Where(key => key != TenantProperty(type))]);
    }

    // The required properties the default seeder fills: not generated by the database, not the tenant
    // property or a key that gets the tenant, not part of a foreign key the probe points at a principal
    // (a required one, or one that holds the tenant property), and not the discriminator.
    private IEnumerable<IProperty> FilledProperties(IEntityType type)
    {
        var tenantProperty = entities.TryGet(type, out var tenant) ? tenant.Property : null;
        var tenantKeys = TenantKeys(type);
        var discriminator = type.FindDiscriminatorProperty();
        return type.GetProperties().Where(property =>
            !property.IsNullable
            && property.ValueGenerated == ValueGenerated.Never
            && property != tenantProperty
            && !tenantKeys.Contains(property)
            && property != discriminator
            && !property.GetContainingForeignKeys().Any(foreignKey =>
                !foreignKey.IsOwnership && (foreignKey.IsRequired || (tenantProperty is not null && foreignKey.Properties.Contains(tenantProperty)))));
    }

    // Creates each required owned reference left null, and fills the required values left null in every
    // owned reference: an owned row is saved with its owner.
    private void FillOwned(IEntityType type, object entity, DbContext context)
    {
        foreach (var navigation in OwnedReferences(type))
        {
            var owned = GetClrValue(navigation, entity);
            if (owned is null)
            {
                if (!navigation.ForeignKey.IsRequiredDependent)
                {
                    continue;
                }

                var filled = ConstructorProperties(navigation.TargetEntityType).ToDictionary(property => property, generator.Next);
                owned = Instantiate(navigation.TargetEntityType, context, property => filled.GetValueOrDefault(property));
                TrySetClrValue(navigation, entity, owned, onlyIfUnset: false);
            }

            var ownedType = navigation.TargetEntityType;
            foreach (var property in OwnedProperties(ownedType))
            {
                if (GetClrValue(property, owned) is null)
                {
                    TrySetClrValue(property, owned, generator.Next(property), onlyIfUnset: false);
                }
            }

            FillOwned(ownedType, owned, context);
        }
    }
}
