// <copyright file="CleanDbContextFactory.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging;
using TenantGuard;

namespace ValidateTool.Fixture;

// Logs to the console, under Enforcement.Log, so the tool's output shows whether TenantGuard's own log
// of the model check is muted while the tool runs.
public sealed class CleanDbContextFactory : IDesignTimeDbContextFactory<CleanDbContext>
{
    public CleanDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<CleanDbContext>()
            .UseSqlite("DataSource=:memory:")
            .UseLoggerFactory(LoggerFactory.Create(logging => logging.AddProvider(new ConsoleLines())))
            .UseTenantGuard(tg => tg.Enforcement(Enforcement.Log).CurrentTenant(context => ((CleanDbContext)context).TenantId))
            .Options);
}
