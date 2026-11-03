// <copyright file="ContextTypes.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Tool;

/// <summary>
/// Finds the <c>DbContext</c> types in the app's assembly and picks the one to validate.
/// </summary>
internal static class ContextTypes
{
    /// <summary>
    /// Picks the context: the one <paramref name="name"/> names, by name or full name, or the only one.
    /// </summary>
    /// <param name="assembly">The assembly that holds the context.</param>
    /// <param name="name">The context's name or full name, or null.</param>
    /// <param name="contextType">The context type, when one was picked.</param>
    /// <param name="error">Why none was, otherwise.</param>
    /// <returns>Whether a context was picked.</returns>
    public static bool TrySelect(Assembly assembly, string? name, out Type? contextType, out string? error)
    {
        var types = Find(assembly);
        var assemblyName = assembly.GetName().Name;
        var listed = string.Join(", ", types.Select(type => type.Name));
        contextType = null;
        error = null;

        if (types.Count == 0)
        {
            error = $"{assemblyName} has no DbContext type. Pass the assembly that holds the context with --assembly.";
            return false;
        }

        if (name is null)
        {
            if (types.Count > 1)
            {
                error = $"The assembly has {types.Count} DbContext types: {listed}. Pass --context to pick one.";
                return false;
            }

            contextType = types[0];
            return true;
        }

        var matches = types.Where(type => type.FullName == name).ToList();
        if (matches.Count == 0)
        {
            matches = types.Where(type => type.Name == name).ToList();
        }

        if (matches.Count == 1)
        {
            contextType = matches[0];
            return true;
        }

        error = matches.Count == 0
            ? $"{assemblyName} has no DbContext type named '{name}'. Its DbContext types: {listed}."
            : $"More than one DbContext type is named '{name}': {string.Join(", ", matches.Select(type => type.FullName))}. Pass the full name to --context.";
        return false;
    }

    /// <summary>
    /// Lists the assembly's concrete <c>DbContext</c> types, by full name.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The context types.</returns>
    internal static IReadOnlyList<Type> Find(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types;
        }

        return [.. types
            .OfType<Type>()
            .Where(type => typeof(DbContext).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters)
            .OrderBy(type => type.FullName, StringComparer.Ordinal),
        ];
    }
}
