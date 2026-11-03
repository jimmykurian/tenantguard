// <copyright file="ParseResult.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// The result of parsing: the arguments, a request for help, or an error to print.
/// </summary>
/// <param name="Arguments">The parsed arguments, when parsing succeeded.</param>
/// <param name="Help">Whether <c>--help</c> was given.</param>
/// <param name="Error">What was wrong, when parsing failed.</param>
internal sealed record ParseResult(ValidateArguments? Arguments, bool Help, string? Error);
