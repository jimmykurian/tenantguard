// <copyright file="TenantGuardLogging.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TenantGuard;

// Logs through the application's ILoggerFactory when there is one, else NullLogger. Category TenantGuard.
internal static class TenantGuardLogging
{
    public const string Category = "TenantGuard";

    public static ILogger CreateLogger(IDbContextOptions contextOptions)
    {
        var core = contextOptions.FindExtension<CoreOptionsExtension>();
        var factory = core?.LoggerFactory ?? core?.ApplicationServiceProvider?.GetService<ILoggerFactory>();
        return factory?.CreateLogger(Category) ?? NullLogger.Instance;
    }
}
