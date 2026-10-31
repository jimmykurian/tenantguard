// <copyright file="RowFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// Creates one row of an entity type for a tenant: with the seeder Seed<T> set, else with every
// required value the database does not generate filled. Either way the tenant property gets the tenant,
// and a required foreign key left unset points at the principal row seeded for the same tenant, or at
// the one shared row of a principal that is not tenant-scoped.
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

        if (type.ClrType.GetConstructor(Constructors, Type.EmptyTypes) is null)
        {
            return $"{type.ClrType.Name} has no parameterless constructor";
        }

        if (type.GetComplexProperties().FirstOrDefault(complex => !complex.IsNullable) is { } required)
        {
            return $"the default seeder does not fill the required complex property {required.Name}";
        }

        if (FilledProperties(type).FirstOrDefault(property => !ValueGenerator.CanFill(property)) is { } unfillable)
        {
            return $"the default seeder cannot fill the required {unfillable.ClrType.Name} property {unfillable.Name}";
        }

        return OwnedTypesToCreate(type).FirstOrDefault(owned => owned.ClrType.GetConstructor(Constructors, Type.EmptyTypes) is null) is { } ownedType
            ? $"the owned type {ownedType.ClrType.Name} has no parameterless constructor"
            : null;
    }

    // Creates the row and adds it to the context; the caller saves.
    public EntityEntry Add(TContext context, IEntityType type, string tenant, IEnumerable<(IForeignKey ForeignKey, SeededRow Principal)> principals)
    {
        var seeded = seeders.TryGetValue(type.ClrType, out var seed);
        var entity = seeded ? seed!(tenant, context) : Activator.CreateInstance(type.ClrType, nonPublic: true)!;

        var tenantProperty = entities.TryGet(type, out var tenantMetadata) ? tenantMetadata.Property : null;
        var values = new List<(IProperty Property, object? Value, bool OnlyIfUnset)>();
        if (tenantProperty is not null)
        {
            values.Add((tenantProperty, TenantValues.Convert(tenant, tenantProperty), false));
        }

        foreach (var (foreignKey, principal) in principals)
        {
            for (var index = 0; index < foreignKey.Properties.Count; index++)
            {
                if (foreignKey.Properties[index] != tenantProperty)
                {
                    values.Add((foreignKey.Properties[index], principal.Values[foreignKey.PrincipalKey.Properties[index]], seeded));
                }
            }
        }

        if (!seeded)
        {
            values.AddRange(FilledProperties(type).Select(property => (property, generator.Next(property), false)));
            FillOwned(type, entity);
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
            var current = entry.Property(property);
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

    // The required properties the default seeder fills: not generated by the database, not the tenant
    // property, not part of a required foreign key, and not the discriminator.
    private IEnumerable<IProperty> FilledProperties(IEntityType type)
    {
        var tenantProperty = entities.TryGet(type, out var tenant) ? tenant.Property : null;
        var discriminator = type.FindDiscriminatorProperty();
        return type.GetProperties().Where(property =>
            !property.IsNullable
            && property.ValueGenerated == ValueGenerated.Never
            && property != tenantProperty
            && property != discriminator
            && !property.GetContainingForeignKeys().Any(foreignKey => foreignKey.IsRequired && !foreignKey.IsOwnership));
    }

    // Creates each required owned reference left null, and fills the required values left null in every
    // owned reference: an owned row is saved with its owner.
    private void FillOwned(IEntityType type, object entity)
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

                owned = Activator.CreateInstance(navigation.TargetEntityType.ClrType, nonPublic: true)!;
                TrySetClrValue(navigation, entity, owned, onlyIfUnset: false);
            }

            var ownedType = navigation.TargetEntityType;
            foreach (var property in ownedType.GetProperties().Where(property => !property.IsNullable && !property.IsShadowProperty() && !property.IsKey() && property.ValueGenerated == ValueGenerated.Never))
            {
                if (GetClrValue(property, owned) is null)
                {
                    TrySetClrValue(property, owned, generator.Next(property), onlyIfUnset: false);
                }
            }

            FillOwned(ownedType, owned);
        }
    }
}
