// <copyright file="AppLayout.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Text.Json;

namespace TenantGuard.Tool;

/// <summary>
/// What the tool needs from the app's build output: the files <c>dotnet exec</c> runs the payload
/// under, the app's target framework, and the version of TenantGuard it references.
/// </summary>
/// <param name="DepsFile">The startup assembly's <c>.deps.json</c>.</param>
/// <param name="RuntimeConfig">The startup assembly's <c>.runtimeconfig.json</c>.</param>
/// <param name="TargetFramework">The app's target framework, such as <c>net10.0</c>.</param>
/// <param name="TenantGuardVersion">The <c>TenantGuard.EntityFrameworkCore</c> version the app references, or null.</param>
internal sealed record AppLayout(string DepsFile, string RuntimeConfig, string TargetFramework, string? TenantGuardVersion)
{
    /// <summary>
    /// The target frameworks the tool ships a payload for.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedTargetFrameworks = ["net10.0", "net11.0"];

    private const string RuntimePackage = "TenantGuard.EntityFrameworkCore";

    /// <summary>
    /// Reads the build output next to the startup assembly.
    /// </summary>
    /// <param name="startupAssembly">The full path of the app's entry assembly.</param>
    /// <param name="layout">The layout, when it could be read.</param>
    /// <param name="error">What's missing, when it couldn't.</param>
    /// <returns>Whether the layout could be read.</returns>
    public static bool TryRead(string startupAssembly, out AppLayout? layout, out string? error)
    {
        layout = null;
        var name = Path.GetFileName(startupAssembly);
        var depsFile = Path.ChangeExtension(startupAssembly, ".deps.json");
        var runtimeConfig = Path.ChangeExtension(startupAssembly, ".runtimeconfig.json");
        foreach (var file in new[] { depsFile, runtimeConfig })
        {
            if (!File.Exists(file))
            {
                error = $"{Path.GetFileName(file)} isn't next to {name}. Pass the app's build output, as dotnet build writes it, or name the app's entry assembly with --startup-assembly.";
                return false;
            }
        }

        string? targetFramework;
        string? tenantGuardVersion;
        try
        {
            targetFramework = ReadTargetFramework(File.ReadAllText(runtimeConfig));
            tenantGuardVersion = ReadPackageVersion(File.ReadAllText(depsFile), RuntimePackage);
        }
        catch (JsonException exception)
        {
            error = $"Couldn't read the build output next to {name}: {exception.Message}";
            return false;
        }

        if (targetFramework is null || !SupportedTargetFrameworks.Contains(targetFramework))
        {
            error = $"{name} targets {targetFramework ?? "an unknown framework"}; the tool supports {string.Join(" and ", SupportedTargetFrameworks)}.";
            return false;
        }

        layout = new AppLayout(depsFile, runtimeConfig, targetFramework, tenantGuardVersion);
        error = null;
        return true;
    }

    /// <summary>
    /// Reads <c>runtimeOptions.tfm</c> from a <c>.runtimeconfig.json</c>.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The target framework, or null when the file doesn't name one.</returns>
    internal static string? ReadTargetFramework(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty("runtimeOptions", out var options)
            && options.TryGetProperty("tfm", out var tfm)
            && tfm.ValueKind == JsonValueKind.String
                ? tfm.GetString()
                : null;
    }

    /// <summary>
    /// Reads a library's version from a <c>.deps.json</c>, whose <c>libraries</c> keys are
    /// <c>name/version</c>, for a package and a project reference alike.
    /// </summary>
    /// <param name="json">The file's text.</param>
    /// <param name="library">The library's name.</param>
    /// <returns>The version, or null when the app doesn't reference the library.</returns>
    internal static string? ReadPackageVersion(string json, string library)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("libraries", out var libraries) || libraries.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var prefix = library + "/";
        foreach (var entry in libraries.EnumerateObject())
        {
            if (entry.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Name[prefix.Length..];
            }
        }

        return null;
    }
}
