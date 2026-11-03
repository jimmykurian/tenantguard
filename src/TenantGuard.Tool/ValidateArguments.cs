// <copyright file="ValidateArguments.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// The options of <c>dotnet tenantguard validate</c>, parsed. The tool and the payload share this
/// file, so the tool passes its options on to the payload in the same form.
/// </summary>
/// <param name="Assembly">The assembly that holds the context.</param>
/// <param name="StartupAssembly">The app's entry assembly; the same as <paramref name="Assembly"/> unless given.</param>
/// <param name="Context">The context's name or full name, or null to take the only one.</param>
/// <param name="WarningsAsErrors">Whether a warning fails the check.</param>
/// <param name="AppArguments">The arguments after <c>--</c>, passed to the app.</param>
internal sealed record ValidateArguments(
    string Assembly,
    string StartupAssembly,
    string? Context,
    bool WarningsAsErrors,
    IReadOnlyList<string> AppArguments);
