// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace TenantGuard.Tool;

/// <summary>
/// The payload's entry point. The tool starts it with <c>dotnet exec</c> under the app's
/// dependencies, after checking the options and the build output.
/// </summary>
internal static class Program
{
    private const string RuntimeAssembly = "TenantGuard.EntityFrameworkCore";

    /// <summary>
    /// Validates the context the arguments name.
    /// </summary>
    /// <param name="args">The tool's arguments, as <see cref="CommandLine.Format"/> writes them.</param>
    /// <returns>The exit code.</returns>
    public static int Main(string[] args)
    {
        // An app that doesn't reference TenantGuard has no copy of it in its dependencies, so it
        // loads from beside the payload. This runs before any method that touches TenantGuard's types.
        var payloadDirectory = AppContext.BaseDirectory;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var path = Path.Combine(payloadDirectory, RuntimeAssembly + ".dll");
            return name.Name == RuntimeAssembly && File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
        };

        return Run(args);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
        => Validation.Run(args, Console.Out, Console.Error, path => Load(path));

    private static Assembly Load(string path)
    {
        // The app's assemblies are on the trusted platform list from its .deps.json; load them by
        // name so they aren't loaded twice, and by path only when the list doesn't have them.
        try
        {
            return Assembly.Load(AssemblyName.GetAssemblyName(path));
        }
        catch (FileNotFoundException)
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        }
    }
}
