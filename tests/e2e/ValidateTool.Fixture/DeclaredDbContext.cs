// <copyright file="DeclaredDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

// Declares its cross-tenant data: Membership is unfiltered, and Note's nullable tenant is suppressed.
public sealed class DeclaredDbContext(DbContextOptions<DeclaredDbContext> options) : DbContext(options)
{
    public string? TenantId { get; set; }

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Membership> Memberships => Set<Membership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Note>().HasQueryFilter(note => note.TenantId == TenantId);
}
