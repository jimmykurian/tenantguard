// <copyright file="TenantValues.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.ComponentModel;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// The probe's tenants are strings. This turns one into a value of a tenant property's type:
// through the property's value converter when it stores strings, otherwise with the type's TypeConverter,
// so "6f9619ff-8b86-d011-b42d-00c04fc964ff" stamps a Guid tenant and "42" an int one.
internal static class TenantValues
{
    public static object Convert(string tenant, IReadOnlyProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (type == typeof(string))
        {
            return tenant;
        }

        try
        {
            var value = property.GetValueConverter() is { ProviderClrType: var stored } converter && stored == typeof(string)
                ? converter.ConvertFromProvider(tenant)
                : TypeDescriptor.GetConverter(type).ConvertFromInvariantString(tenant);
            return value ?? throw new FormatException($"\"{tenant}\" converts to null.");
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or NotSupportedException or InvalidCastException or OverflowException)
        {
            throw new InvalidOperationException(
                $"The tenant \"{tenant}\" is not a {type.Name}, the type of {property.DeclaringType.DisplayName()}.{property.Name}. Pass WithTenants values the tenant property can hold.",
                exception);
        }
    }
}
