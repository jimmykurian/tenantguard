// <copyright file="CompositeForeignKeyTenantPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/change-tracking/relationship-changes#optional-relationships
[FeatureDescription("A foreign key that includes the tenant column makes the database refuse a link across tenants. When an optional one is cleared through its navigation, EF Core sets every foreign key property that can hold null to null, a string tenant property included, and setting the other foreign key property to null leaves the tenant alone. A row with a null tenant isn't checked. TGM015's fix rests on these.")]
public sealed class CompositeForeignKeyTenantPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private Exception? _crossTenantSave;
    private string? _tenantAfterClearingTheNavigation;
    private string? _tenantAfterClearingTheForeignKey;
    private Exception? _hostSave;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Composite_foreign_key_with_the_tenant_refuses_a_link_across_tenants()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_a_customer_and_acme_has_an_invoice_for_its_own,
            When_an_acme_invoice_is_saved_pointing_at_globexs_customer,
            Then_the_database_refuses_it);

    [Fact]
    public Task Clearing_the_navigation_nulls_a_string_tenant_and_clearing_the_key_does_not()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_a_customer_and_acme_has_an_invoice_for_its_own,
            When_the_invoices_customer_is_cleared_through_the_navigation_and_through_the_key,
            Then_the_navigation_nulled_the_tenant,
            Then_the_key_left_the_tenant_alone);

    [Fact]
    public Task Row_with_a_null_tenant_is_not_checked()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_a_customer_and_acme_has_an_invoice_for_its_own,
            When_an_invoice_with_no_tenant_is_saved_pointing_at_globexs_customer,
            Then_the_database_accepts_it);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_acme_and_globex_each_have_a_customer_and_acme_has_an_invoice_for_its_own()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        await using var setup = new PinContext(_options);
        await setup.Database.EnsureCreatedAsync(Token);
        setup.Customers.AddRange(new Customer { Id = 1, TenantId = "acme" }, new Customer { Id = 2, TenantId = "globex" });
        setup.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", CustomerId = 1 });
        await setup.SaveChangesAsync(Token);
    }

    private async Task When_an_acme_invoice_is_saved_pointing_at_globexs_customer()
    {
        await using var context = new PinContext(_options);
        context.Invoices.Add(new Invoice { Id = 2, TenantId = "acme", CustomerId = 2 });
        _crossTenantSave = await Record.ExceptionAsync(() => context.SaveChangesAsync(Token));
    }

    private Task Then_the_database_refuses_it()
    {
        _crossTenantSave.Should().BeOfType<DbUpdateException>().Which.InnerException!.Message.Should().Contain("FOREIGN KEY constraint failed");
        return Task.CompletedTask;
    }

    private async Task When_the_invoices_customer_is_cleared_through_the_navigation_and_through_the_key()
    {
        await using (var context = new PinContext(_options))
        {
            var invoice = await context.Invoices.Include(row => row.Customer).SingleAsync(Token);
            invoice.Customer = null;
            context.ChangeTracker.DetectChanges();
            _tenantAfterClearingTheNavigation = invoice.TenantId;
        }

        await using (var context = new PinContext(_options))
        {
            var invoice = await context.Invoices.Include(row => row.Customer).SingleAsync(Token);
            invoice.CustomerId = null;
            await context.SaveChangesAsync(Token);
            _tenantAfterClearingTheForeignKey = invoice.TenantId;
        }
    }

    private Task Then_the_navigation_nulled_the_tenant()
    {
        _tenantAfterClearingTheNavigation.Should().BeNull();
        return Task.CompletedTask;
    }

    private Task Then_the_key_left_the_tenant_alone()
    {
        _tenantAfterClearingTheForeignKey.Should().Be("acme");
        return Task.CompletedTask;
    }

    private async Task When_an_invoice_with_no_tenant_is_saved_pointing_at_globexs_customer()
    {
        await using var context = new PinContext(_options);
        context.Invoices.Add(new Invoice { Id = 2, TenantId = null, CustomerId = 2 });
        _hostSave = await Record.ExceptionAsync(() => context.SaveChangesAsync(Token));
    }

    private Task Then_the_database_accepts_it()
    {
        _hostSave.Should().BeNull();
        return Task.CompletedTask;
    }

    private sealed class PinContext(DbContextOptions<PinContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Customer>().Property(customer => customer.Id).ValueGeneratedNever();
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.Property(row => row.Id).ValueGeneratedNever();
                invoice.HasOne(row => row.Customer).WithMany()
                    .HasForeignKey(row => new { row.TenantId, row.CustomerId })
                    .HasPrincipalKey(customer => new { customer.TenantId, customer.Id });
            });
        }
    }

    private sealed class Customer
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }

        public string? TenantId { get; set; }

        public int? CustomerId { get; set; }

        public Customer? Customer { get; set; }
    }
}
