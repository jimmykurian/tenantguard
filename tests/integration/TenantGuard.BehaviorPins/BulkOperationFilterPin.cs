// <copyright file="BulkOperationFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// Query filters apply to ExecuteUpdate and ExecuteDelete, so a bulk statement only
// touches the current tenant's rows.
// https://learn.microsoft.com/ef/core/saving/execute-insert-update-delete
public sealed class BulkOperationFilterPin
{
    [Fact]
    public async Task ExecuteUpdate_and_ExecuteDelete_touch_only_the_current_tenants_rows()
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
                new Invoice { Id = 2, TenantId = "acme", Number = "acme-2" },
                new Invoice { Id = 3, TenantId = "globex", Number = "globex-1" });
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using (var globex = new PinContext(options, "globex"))
        {
            Assert.Equal(1, await globex.Invoices.ExecuteUpdateAsync(setters => setters.SetProperty(invoice => invoice.Number, "changed"), cancellationToken));
            Assert.Equal(1, await globex.Invoices.ExecuteDeleteAsync(cancellationToken));
        }

        await using var acme = new PinContext(options, "acme");
        Assert.Collection(
            await acme.Invoices.OrderBy(invoice => invoice.Id).Select(invoice => invoice.Number).ToListAsync(cancellationToken),
            number => Assert.Equal("acme-1", number),
            number => Assert.Equal("acme-2", number));
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
