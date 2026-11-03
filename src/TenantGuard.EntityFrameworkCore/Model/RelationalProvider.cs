// <copyright file="RelationalProvider.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Tells whether a context or a model uses a relational provider, which TGM009 and TGM010 need.
// The relational types are matched by name, so the runtime package needs no relational reference.
internal static class RelationalProvider
{
    private const string OptionsExtension = "Microsoft.EntityFrameworkCore.Infrastructure.RelationalOptionsExtension";

    private const string AnnotationPrefix = "Relational:";

    // Every relational provider's options extension derives from RelationalOptionsExtension.
    public static bool Uses(IDbContextOptions contextOptions)
        => contextOptions.Extensions.Any(extension => DerivesFrom(extension.GetType(), OptionsExtension));

    // A runtime model built by a relational provider maps each entity type to a table, view or
    // query, and keeps that mapping as Relational: annotations.
    public static bool Built(IReadOnlyModel model)
        => model.GetEntityTypes().Any(entityType => entityType.GetAnnotations().Any(annotation => annotation.Name.StartsWith(AnnotationPrefix, StringComparison.Ordinal)));

    private static bool DerivesFrom(Type? type, string fullName)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.FullName == fullName)
            {
                return true;
            }
        }

        return false;
    }
}
