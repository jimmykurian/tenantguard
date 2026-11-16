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

    // The filter rules' models have no tenant index and single-column foreign keys, so this leaves
    // out TGM009, TGM010 and TGM015; ValidateAll keeps them.
    public static ModelReport Validate<TContext>(Action<TenantGuardOptionsBuilder>? configure = null)
        where TContext : DbContext
        => new([.. ValidateAll<TContext>(configure).Findings.Where(finding => finding.RuleId is not ("TGM009" or "TGM010" or "TGM015"))]);

    // The index rules' models have single-column foreign keys, so this leaves out TGM015.
    public static ModelReport ValidateIndexes<TContext>(Action<TenantGuardOptionsBuilder>? configure = null)
        where TContext : DbContext
        => new([.. ValidateAll<TContext>(configure).Findings.Where(finding => finding.RuleId is not "TGM015")]);

    public static ModelReport ValidateAll<TContext>(Action<TenantGuardOptionsBuilder>? configure = null)
        where TContext : DbContext
    {
        var options = new TenantGuardOptionsBuilder();
        configure?.Invoke(options);
        using var context = Create<TContext>();
        return TenantGuardModel.Validate(context.Model, options.Options);
    }

    // A model built with EF Core's own conventions and no provider, as a non-relational provider
    // builds one: it has no relational annotations.
    public static ModelReport ValidateWithoutProvider(Action<ModelBuilder> build)
    {
        var modelBuilder = new ModelBuilder();
        build(modelBuilder);
        return TenantGuardModel.Validate(modelBuilder.FinalizeModel(), new TenantGuardOptionsBuilder().Options);
    }
}
