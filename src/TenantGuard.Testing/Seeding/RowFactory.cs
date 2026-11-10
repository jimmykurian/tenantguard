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

        foreach (var owned in OwnedTypesToCreate(type))
        {
            if (!CanInstantiate(owned))
            {
                return $"the owned type {owned.ClrType.Name} has no parameterless constructor, and EF Core doesn't bind one of its constructors";
            }

            if (OwnedPropertiesToFill(owned).Select(ValueGenerator.WhyCannotFill).FirstOrDefault(reason => reason is not null) is { } ownedUnfillable)
            {
                return ownedUnfillable;
            }
        }

        return null;
    }

    // The keys of the type that a tenant value reaches as a foreign key, other than the type's own
    // tenant property: each row seeded for a tenant gets the tenant in them. Another type's tenant
    // property points at them, or a key of another type that a tenant value reaches in turn, as along
    // a shared primary key (Role.TenantId to Tenant.Id, which is the foreign key to Organization.Id).
    public IReadOnlyList<IProperty> TenantKeys(IEntityType type)
    {
        var ownTenant = TenantProperty(type);
        return [.. KeysATenantReaches(type, []).Where(key => key != ownTenant).Distinct()];
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

    // The owned values the seeder has to generate: the constructor's, and the others a new instance
    // leaves null. A value the constructor or an initializer sets is kept, so it needn't be fillable.
    // When the instance can't be made here, every other value counts.
    private static IEnumerable<IProperty> OwnedPropertiesToFill(IEntityType owned)
    {
        var arguments = ConstructorProperties(owned);
        var sampler = new ValueGenerator();
        object? sample;
        try
        {
            // As FillOwned does: a parameter outside the arguments, such as a nullable one, gets its default.
            sample = arguments.Any(property => ValueGenerator.WhyCannotFill(property) is not null)
                ? null
                : Instantiate(owned, context: null, property => arguments.Contains(property) ? sampler.Next(property) : null);
        }
        catch (Exception exception) when (exception is TargetInvocationException or ArgumentException or MemberAccessException)
        {
            sample = null;
        }

        return arguments.Concat(OwnedProperties(owned).Except(arguments).Where(property => sample is null || GetClrValue(property, sample) is null));
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

    // The owned types reached through required owned references, which the default seeder creates.
    private static IEnumerable<IEntityType> OwnedTypesToCreate(IEntityType type)
        => OwnedReferences(type).Where(navigation => navigation.ForeignKey.IsRequiredDependent)
            .SelectMany(navigation => OwnedTypesToCreate(navigation.TargetEntityType).Prepend(navigation.TargetEntityType));

    private static IEnumerable<INavigation> OwnedReferences(IEntityType type)
        => type.GetNavigations().Where(navigation => !navigation.IsCollection && navigation.ForeignKey.IsOwnership && !navigation.IsOnDependent);

    // The keys of the type that a dependent's tenant property, or a key a tenant value reaches in the
    // dependent, points at. visited stops a cycle of relationships.
    private List<IProperty> KeysATenantReaches(IEntityType type, HashSet<IEntityType> visited)
    {
        if (!visited.Add(type))
        {
            return [];
        }

        var keys = new List<IProperty>();
        foreach (var foreignKey in type.GetReferencingForeignKeys().Where(foreignKey => !foreignKey.IsOwnership))
        {
            var dependent = foreignKey.DeclaringEntityType;
            var tenantValued = KeysATenantReaches(dependent, [.. visited]).Append(TenantProperty(dependent)).OfType<IProperty>().ToHashSet();
            keys.AddRange(foreignKey.Properties
                .Select((property, index) => (property, index))
                .Where(pair => tenantValued.Contains(pair.property))
                .Select(pair => foreignKey.PrincipalKey.Properties[pair.index]));
        }

        return keys;
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
