// <copyright file="TenantValueFormatter.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;

namespace TenantGuard;

// Formats the only values TenantGuard puts in messages and logs, tenant ids and key values
// (AGENTS.md), the same way everywhere: strings quoted, numbers in the invariant culture.
internal static class TenantValueFormatter
{
    public static string Format(object? value)
        => value switch
        {
            null => "null",
            string text => $"\"{text}\"",
            char character => $"'{character}'",
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
}
