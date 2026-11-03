// <copyright file="ValidateCommand.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;

namespace TenantGuard.Tool;

/// <summary>
/// <c>dotnet tenantguard validate</c>: checks the options and the app's build output, then runs the
/// payload inside the app's runtime and dependencies, which does the validating (ADR 0024).
/// </summary>
internal static class ValidateCommand
{
    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="args">The command line, without the program name.</param>
    /// <param name="output">Where help goes.</param>
    /// <param name="error">Where a reason the tool couldn't run goes.</param>
    /// <param name="runner">Starts the payload.</param>
    /// <param name="toolVersion">The tool's version, compared with the app's TenantGuard.</param>
    /// <param name="toolDirectory">The directory that holds the tool and its payloads.</param>
    /// <returns>The exit code.</returns>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        IPayloadRunner runner,
        string toolVersion,
        string toolDirectory)
    {
        var parsed = CommandLine.Parse(args);
        if (parsed.Help)
        {
            output.WriteLine(CommandLine.Usage);
            return ExitCodes.Passed;
        }

        if (parsed.Arguments is not { } arguments)
        {
            error.WriteLine(parsed.Error);
            return ExitCodes.CouldNotValidate;
        }

        arguments = arguments with
        {
            Assembly = Path.GetFullPath(arguments.Assembly),
            StartupAssembly = Path.GetFullPath(arguments.StartupAssembly),
        };
        foreach (var path in new[] { arguments.Assembly, arguments.StartupAssembly }.Distinct())
        {
            if (!File.Exists(path))
            {
                error.WriteLine($"Assembly not found: {path}. Build the app first, and pass the path to its .dll.");
                return ExitCodes.CouldNotValidate;
            }
        }

        if (!AppLayout.TryRead(arguments.StartupAssembly, out var layout, out var layoutError))
        {
            error.WriteLine(layoutError);
            return ExitCodes.CouldNotValidate;
        }

        if (layout!.TenantGuardVersion is { } appVersion
            && !string.Equals(appVersion, toolVersion, StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine(
                $"The app references TenantGuard.EntityFrameworkCore {appVersion}, but this is TenantGuard.Tool {toolVersion}. "
                + $"Install the matching tool: dotnet tool update --global TenantGuard.Tool --version {appVersion}");
            return ExitCodes.CouldNotValidate;
        }

        var payload = Path.Combine(toolDirectory, "payload", layout.TargetFramework, "TenantGuard.Tool.Payload.dll");
        if (!File.Exists(payload))
        {
            error.WriteLine($"The tool's {layout.TargetFramework} payload is missing: {payload}. Reinstall TenantGuard.Tool.");
            return ExitCodes.CouldNotValidate;
        }

        var execArguments = new List<string> { "exec", "--depsfile", layout.DepsFile, "--runtimeconfig", layout.RuntimeConfig };
        if (NuGetPackagesFolder() is { } packages)
        {
            execArguments.Add("--additionalprobingpath");
            execArguments.Add(packages);
        }

        execArguments.Add(payload);
        execArguments.AddRange(CommandLine.Format(arguments));

        try
        {
            return runner.Run(DotnetHost(), execArguments);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error.WriteLine($"Couldn't start dotnet to run the app: {exception.Message}");
            return ExitCodes.CouldNotValidate;
        }
    }

    /// <summary>
    /// The tool's version, as its package states it, without the source revision.
    /// </summary>
    /// <returns>The version, such as <c>0.1.0-preview.8</c>.</returns>
    public static string ToolVersion()
    {
        var version = typeof(ValidateCommand).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }

    // The SDK sets DOTNET_HOST_PATH for the tools it runs; `dotnet TenantGuard.Tool.dll` runs on dotnet itself.
    private static string DotnetHost()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } hostPath)
        {
            return hostPath;
        }

        var processPath = Environment.ProcessPath;
        return processPath is not null && Path.GetFileNameWithoutExtension(processPath) == "dotnet" ? processPath : "dotnet";
    }

    // Lets the app's packages load from the NuGet cache when its build output doesn't copy them, as dotnet ef does.
    private static string? NuGetPackagesFolder()
    {
        var folder = Environment.GetEnvironmentVariable("NUGET_PACKAGES") is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        return Directory.Exists(folder) ? folder : null;
    }
}
