// <copyright file="TenantGuardConventionSetPlugin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace TenantGuard;

// Registered by UseTenantGuard. Appends the model convention, so it sees the model EF hands to
// queries. The context being built tells the rules which filter operands EF reads from the context.
internal sealed class TenantGuardConventionSetPlugin(IDbContextOptions contextOptions, ICurrentDbContext currentContext) : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        if (contextOptions.FindExtension<TenantGuardOptionsExtension>() is { } extension)
        {
            conventionSet.ModelFinalizedConventions.Add(
                new TenantModelConvention(extension.Options, TenantGuardLogging.CreateLogger(contextOptions), currentContext.Context.GetType()));
        }

        return conventionSet;
    }
}
