// <copyright file="TestModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Builds the test contexts on SQLite. Building a model does not open the connection.
internal static class TestModels
{
    public static TContext Create<TContext>(Action<DbContextOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var builder = new DbContextOptionsBuilder().UseSqlite("DataSource=:memory:");
        configure?.Invoke(builder);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    public static ModelReport Validate<TContext>(Action<TenantGuardOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var options = new TenantGuardOptionsBuilder();
        configure?.Invoke(options);
        using var context = Create<TContext>();
        return TenantGuardModel.Validate(context.Model, options.Options);
    }
}
