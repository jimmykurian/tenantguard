// <copyright file="ValueGenerator.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// Deterministic values for the properties the default seeder fills (SPEC 7). A counter runs through
// every value, so no two rows collide on a unique index. A property whose type it cannot fill is
// filled through its value converter's provider type when it has one.
internal sealed class ValueGenerator
{
    private static readonly DateTime _epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private int _next;

    public static bool CanFill(IReadOnlyProperty property)
        => Create(Unwrap(property.ClrType), 1, maxLength: null) is not null
            || (property.GetValueConverter() is { } converter && Create(Unwrap(converter.ProviderClrType), 1, maxLength: null) is not null);

    public object? Next(IReadOnlyProperty property)
    {
        var n = ++_next;
        var maxLength = property.GetMaxLength();
        if (Create(Unwrap(property.ClrType), n, maxLength) is { } value)
        {
            return value;
        }

        return property.GetValueConverter() is { } converter && Create(Unwrap(converter.ProviderClrType), n, maxLength) is { } stored
            ? converter.ConvertFromProvider(stored)
            : null;
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
            TypeCode.Boolean => n % 2 == 1,
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

        return type == typeof(byte[]) ? BitConverter.GetBytes(n) : null;
    }
}
