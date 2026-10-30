// <copyright file="ConventionModels.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// Models for the convention tests that need their own context types, so no other test has
// already built and cached their model.
internal static class ConventionModels
{
    // Two errors, for the exception that lists every error.
    internal sealed class TwoUnguarded(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Invoice>();
            modelBuilder.Entity<Receipt>();
        }
    }

    // One error, built only through the application's service provider.
    internal sealed class AppLogged(DbContextOptions options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>();
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }

    internal sealed class Receipt
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;
    }
}
