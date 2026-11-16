// <copyright file="CurrentTenantGenerator.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

// Sets TenantId to the context's tenant when a row is added to the context. Customer's TenantId is
// part of an alternate key, and EF Core needs a key's values to track a row, which is before
// SaveChanges, where TenantGuard's StampOnAdd would set it.
internal sealed class CurrentTenantGenerator : ValueGenerator<string>
{
    public override bool GeneratesTemporaryValues => false;

    public override string Next(EntityEntry entry)
        => ((AppDbContext)entry.Context).TenantInfo?.Id
            ?? throw new InvalidOperationException($"A {entry.Metadata.DisplayName()} can't be added without a current tenant.");
}
