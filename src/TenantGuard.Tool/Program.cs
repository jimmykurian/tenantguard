// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// The entry point of <c>dotnet tenantguard</c>.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="args">The command line.</param>
    /// <returns>The exit code: 0, 1 or 2, as docs/reference/validate-tool.md lists them.</returns>
    public static int Main(string[] args)
        => ValidateCommand.Run(args, Console.Out, Console.Error, new PayloadRunner(), ValidateCommand.ToolVersion(), AppContext.BaseDirectory);
}
