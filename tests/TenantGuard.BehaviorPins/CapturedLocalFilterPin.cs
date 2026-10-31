// <copyright file="CapturedLocalFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// A filter that captures a local in OnModelCreating freezes the first tenant into the
// model, and EF caches the model per context type, so a context built for another tenant still sees
// the first tenant's rows. This is the leak TGM003 exists for.
// https://learn.microsoft.com/ef/core/querying/filters
public sealed class CapturedLocalFilterPin
{
    [Fact]
    public async Task Second_tenant_sees_the_first_tenants_rows_and_TGM003_flags_the_model()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PinContext>().UseSqlite(connection).Options;

        await using (var acme = new PinContext(options, "acme"))
        {
            await acme.Database.EnsureCreatedAsync(cancellationToken);
            acme.Invoices.AddRange(
                new Invoice { TenantId = "acme", Number = "acme-1" },
                new Invoice { TenantId = "globex", Number = "globex-1" });
            await acme.SaveChangesAsync(cancellationToken);
        }

        await using var globex = new PinContext(options, "globex");
        var visible = await globex.Invoices.Select(invoice => invoice.TenantId).ToListAsync(cancellationToken);

        Assert.Equal("acme", Assert.Single(visible));

        var report = TenantGuardModel.Validate(globex.Model, new TenantGuardOptionsBuilder().Options);
        Assert.Equal("TGM003", Assert.Single(report.Errors).RuleId);
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var captured = tenant;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == captured);
        }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
