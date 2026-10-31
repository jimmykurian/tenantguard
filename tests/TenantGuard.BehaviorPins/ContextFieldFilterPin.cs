// <copyright file="ContextFieldFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// A filter that references a field of the context is evaluated per context instance,
// so two contexts for two tenants each see only their own rows. TGM003 relies on this being the
// safe pattern.
// https://learn.microsoft.com/ef/core/querying/filters
public sealed class ContextFieldFilterPin
{
    private interface ITenantSource
    {
        string TenantId { get; }
    }

    [Fact]
    public async Task Each_context_sees_only_its_own_tenants_rows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PinContext>().UseSqlite(connection).Options;

        await using (var setup = new PinContext(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
            setup.Invoices.AddRange(
                new Invoice { TenantId = "acme", Number = "acme-1" },
                new Invoice { TenantId = "globex", Number = "globex-1" });
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var acme = new PinContext(options, "acme");
        await using var globex = new PinContext(options, "globex");

        Assert.Equal("acme", Assert.Single(await acme.Invoices.Select(invoice => invoice.TenantId).ToListAsync(cancellationToken)));
        Assert.Equal("globex", Assert.Single(await globex.Invoices.Select(invoice => invoice.TenantId).ToListAsync(cancellationToken)));
        Assert.Empty(TenantGuardModel.Validate(globex.Model, new TenantGuardOptionsBuilder().Options).Findings);
    }

    // Finbuckle's pattern: the filter reads a captured null of an interface the context implements,
    // and EF puts the current context in its place, so TGM003 reads it as the context.
    [Fact]
    public async Task Captured_interface_the_context_implements_is_read_from_each_context()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<InterfaceContext>().UseSqlite(connection).Options;

        await using (var setup = new InterfaceContext(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
            setup.Invoices.AddRange(
                new Invoice { TenantId = "acme", Number = "acme-1" },
                new Invoice { TenantId = "globex", Number = "globex-1" });
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var acme = new InterfaceContext(options, "acme");
        await using var globex = new InterfaceContext(options, "globex");

        Assert.Equal("acme", Assert.Single(await acme.Invoices.Select(invoice => invoice.TenantId).ToListAsync(cancellationToken)));
        Assert.Equal("globex", Assert.Single(await globex.Invoices.Select(invoice => invoice.TenantId).ToListAsync(cancellationToken)));
        Assert.Empty(TenantGuardModel.Validate(globex.Model, new TenantGuardOptionsBuilder().Options).Findings);
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == _tenant);
    }

    private sealed class InterfaceContext(DbContextOptions<InterfaceContext> options, string tenant) : DbContext(options), ITenantSource
    {
        public string TenantId { get; } = tenant;

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ITenantSource? context = null;
            modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == context!.TenantId);
        }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public required string Number { get; set; }
    }
}
