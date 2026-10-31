// <copyright file="FindAsyncFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// Query filters apply to FindAsync when the entity is not already tracked, so one
// tenant cannot load another tenant's row by its key.
// https://learn.microsoft.com/ef/core/querying/filters
public sealed class FindAsyncFilterPin
{
    [Fact]
    public async Task FindAsync_returns_null_for_another_tenants_key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PinContext>().UseSqlite(connection).Options;

        await using (var setup = new PinContext(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
            setup.Invoices.AddRange(
                new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" },
                new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var globex = new PinContext(options, "globex");

        Assert.Null(await globex.Invoices.FindAsync([1], cancellationToken));
        Assert.NotNull(await globex.Invoices.FindAsync([2], cancellationToken));
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
