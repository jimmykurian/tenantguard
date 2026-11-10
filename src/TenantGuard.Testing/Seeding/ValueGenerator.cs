// <copyright file="ValueGenerator.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// Deterministic values for the properties the default seeder fills. A counter runs through
// every value, so no two rows collide on a unique index. A bool is always false, so a flag such
// as IsDeleted doesn't put the row behind a soft-delete filter. A collection, such as a primitive
// collection or a dictionary stored as JSON, is empty, and a string in a json or jsonb column is
// {}. A property whose type it cannot fill is filled through its value converter's provider type
// when it has one, trying a generated value, then {} and [] for a string stored as JSON.
internal sealed class ValueGenerator
{
    private static readonly DateTime _epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private int _next;

    // Why the default seeder can't fill the property, naming it and any value converter, or null.
    public static string? WhyCannotFill(IReadOnlyProperty property)
    {
        try
        {
            return TryCreate(property, 1, out _) ? null : Unfillable(property);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"{Unfillable(property)}, which threw: {exception.GetBaseException().Message.ReplaceLineEndings(" ").TrimEnd('.')}";
        }
    }

    // A C# name for the type, such as Dictionary<String, Object>.
    public static string TypeName(Type type)
    {
        if (type.IsArray)
        {
            return $"{TypeName(type.GetElementType()!)}[]";
        }

        // A type nested in a generic one is generic too, but its own type parameters, if any, are the
        // last arguments, as many as its name's `n suffix says.
        var tick = type.Name.IndexOf('`', StringComparison.Ordinal);
        if (!type.IsGenericType || tick < 0)
        {
            return type.Name;
        }

        var arity = int.Parse(type.Name[(tick + 1)..], CultureInfo.InvariantCulture);
        return $"{type.Name[..tick]}<{string.Join(", ", type.GetGenericArguments()[^arity..].Select(TypeName))}>";
    }

    public object? Next(IReadOnlyProperty property)
    {
        TryCreate(property, ++_next, out var value);
        return value;
    }

    private static string Unfillable(IReadOnlyProperty property)
        => property.GetValueConverter() is { } converter
            ? $"the default seeder cannot fill the required {TypeName(property.ClrType)} property {property.Name}, stored through {TypeName(converter.GetType())}"
            : $"the default seeder cannot fill the required {TypeName(property.ClrType)} property {property.Name}";

    // Throws the converter's error when it rejects every value tried.
    private static bool TryCreate(IReadOnlyProperty property, int n, out object? value)
    {
        var maxLength = property.GetMaxLength();
        var type = Unwrap(property.ClrType);
        value = type == typeof(string) && IsJsonColumn(property) ? "{}" : Create(type, n, maxLength);
        if (value is not null)
        {
            return true;
        }

        if (property.GetValueConverter() is not { } converter || Create(Unwrap(converter.ProviderClrType), n, maxLength) is not { } generated)
        {
            return false;
        }

        object?[] candidates = generated is string ? [generated, "{}", "[]"] : [generated];
        Exception? rejected = null;
        foreach (var candidate in candidates)
        {
            try
            {
                value = converter.ConvertFromProvider(candidate);
                if (value is not null)
                {
                    return true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                rejected ??= exception;
            }
        }

        return rejected is null ? false : throw rejected;
    }

    // The column's store type, from the relational annotation or the type mapping, read by name:
    // the probe has no reference to EF Core's relational package.
    private static bool IsJsonColumn(IReadOnlyProperty property)
    {
        var storeType = property.FindAnnotation("Relational:ColumnType")?.Value as string;
        if (storeType is null && property.FindTypeMapping() is { } mapping)
        {
            storeType = mapping.GetType().GetProperty("StoreType")?.GetValue(mapping) as string;
        }

        return storeType is not null
            && (storeType.Equals("json", StringComparison.OrdinalIgnoreCase) || storeType.Equals("jsonb", StringComparison.OrdinalIgnoreCase));
    }

    private static Type Unwrap(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static object? Create(Type type, int n, int? maxLength)
    {
        if (type == typeof(string))
        {
            // The counter is at the end, so a short maximum length keeps the part that differs.
            var text = $"probe-{n}";
            return maxLength is { } max && text.Length > max ? text[^max..] : text;
        }

        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.Length == 0 ? null : values.GetValue(n % values.Length);
        }

        return Type.GetTypeCode(type) switch
        {
            TypeCode.Boolean => false,
            TypeCode.Char => (char)('a' + (n % 26)),
            TypeCode.SByte => (sbyte)(n % sbyte.MaxValue),
            TypeCode.Byte => (byte)(n % byte.MaxValue),
            TypeCode.Int16 => (short)(n % short.MaxValue),
            TypeCode.UInt16 => (ushort)(n % ushort.MaxValue),
            TypeCode.Int32 => n,
            TypeCode.UInt32 => (uint)n,
            TypeCode.Int64 => (long)n,
            TypeCode.UInt64 => (ulong)n,
            TypeCode.Single => (float)n,
            TypeCode.Double => (double)n,
            TypeCode.Decimal => (decimal)n,
            TypeCode.DateTime => _epoch.AddDays(n),
            _ => CreateStruct(type, n),
        };
    }

    private static object? CreateStruct(Type type, int n)
    {
        if (type == typeof(Guid))
        {
            return new Guid(n, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1);
        }

        if (type == typeof(DateTimeOffset))
        {
            return new DateTimeOffset(_epoch).AddDays(n);
        }

        if (type == typeof(DateOnly))
        {
            return DateOnly.FromDateTime(_epoch).AddDays(n);
        }

        if (type == typeof(TimeOnly))
        {
            return TimeOnly.MinValue.Add(TimeSpan.FromMinutes(n));
        }

        if (type == typeof(TimeSpan))
        {
            return TimeSpan.FromMinutes(n);
        }

        return type == typeof(byte[]) ? BitConverter.GetBytes(n) : EmptyCollection(type);
    }

    // An empty array, list, set or dictionary for a collection type, or null.
    private static object? EmptyCollection(Type type)
    {
        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsInterface && type.IsGenericType)
        {
            return ListFor(type) is { } concrete ? Activator.CreateInstance(concrete.MakeGenericType(type.GetGenericArguments())) : null;
        }

        return typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null
            ? Activator.CreateInstance(type)
            : null;
    }

    // The open generic collection to create for a collection interface, or null.
    private static Type? ListFor(Type type)
    {
        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
        {
            return typeof(Dictionary<,>);
        }

        if (definition == typeof(ISet<>) || definition == typeof(IReadOnlySet<>))
        {
            return typeof(HashSet<>);
        }

        Type[] lists = [typeof(IEnumerable<>), typeof(ICollection<>), typeof(IList<>), typeof(IReadOnlyCollection<>), typeof(IReadOnlyList<>)];
        return lists.Contains(definition) ? typeof(List<>) : null;
    }
}
