// <copyright file="FromSqlFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// Query filters compose over FromSqlRaw on an entity type, so analyzer TG002 leaves
// FromSql, FromSqlRaw and FromSqlInterpolated alone. If this fails, they join TG002.
// https://learn.microsoft.com/ef/core/querying/sql-queries
public sealed class FromSqlFilterPin
{
    [Fact]
    public async Task FromSqlRaw_returns_only_the_current_tenants_rows()
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

        var tenants = await globex.Invoices.FromSqlRaw("SELECT * FROM Invoices").Select(invoice => invoice.TenantId).ToListAsync(cancellationToken);
        tenants.Should().Equal("globex");
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().ToTable("Invoices").HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
