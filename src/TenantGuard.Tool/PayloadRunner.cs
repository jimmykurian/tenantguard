// <copyright file="PayloadRunner.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;

namespace TenantGuard.Tool;

/// <summary>
/// Starts the payload and waits for it.
/// </summary>
internal interface IPayloadRunner
{
    /// <summary>
    /// Runs a program to its end.
    /// </summary>
    /// <param name="fileName">The program, here dotnet.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>Its exit code.</returns>
    int Run(string fileName, IReadOnlyList<string> arguments);
}

/// <summary>
/// Runs the payload as a child process that shares the tool's console, so its output goes
/// straight to the tool's standard output and error.
/// </summary>
internal sealed class PayloadRunner : IPayloadRunner
{
    /// <inheritdoc/>
    public int Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"{fileName} didn't start.");
        process.WaitForExit();
        return process.ExitCode;
    }
}
