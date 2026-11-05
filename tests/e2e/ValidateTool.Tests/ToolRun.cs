// <copyright file="ToolRun.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;
using System.Reflection;

namespace ValidateTool.Tests;

// One run of the built tool: its exit code and what it wrote.
internal sealed record ToolRun(int ExitCode, string Output, string Error)
{
    public static string Fixture => Metadata("Fixture");

    public static string UnreferencedFixture => Metadata("UnreferencedFixture");

    public static string Sample(string name) => Metadata("Samples").Replace("SAMPLE", name, StringComparison.Ordinal);

    public static async Task<ToolRun> StartAsync(params string[] args)
    {
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(Metadata("Tool"));
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ToolRun(process.ExitCode, (await output).ReplaceLineEndings("\n"), (await error).ReplaceLineEndings("\n"));
    }

    private static string Metadata(string key)
        => Path.GetFullPath(typeof(ToolRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!);
}
