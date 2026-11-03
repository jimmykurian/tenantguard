// <copyright file="ExitCodes.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// The tool's exit codes, as docs/reference/validate-tool.md lists them. The tool and the payload
/// share this file.
/// </summary>
internal static class ExitCodes
{
    /// <summary>No errors, and no warnings under <c>--warnings-as-errors</c>.</summary>
    public const int Passed = 0;

    /// <summary>An error, or a warning under <c>--warnings-as-errors</c>.</summary>
    public const int Failed = 1;

    /// <summary>The tool couldn't validate.</summary>
    public const int CouldNotValidate = 2;
}
