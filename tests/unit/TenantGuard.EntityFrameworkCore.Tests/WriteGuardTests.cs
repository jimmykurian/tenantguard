// <copyright file="WriteGuardTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Support;
using static TenantGuard.EntityFrameworkCore.Tests.Models.WriteGuardModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// The SaveChanges guard: every state and policy, before anything reaches the database.
public sealed class WriteGuardTests
{
    [Fact]
    public void Insert_for_the_current_tenant_is_saved()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        acme.SaveChanges();

        Assert.Equal("acme", Assert.Single(db.AllInvoices()).TenantId);
    }

    [Fact]
    public void Insert_for_another_tenant_throws_before_the_database_sees_it()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 7, TenantId = "globex", Number = "globex-7" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Equal(
            "TenantGuard blocked 1 write outside the current tenant \"acme\":\n"
                + "- Added Invoice (Id: 7): TenantId is \"globex\"\n"
                + "Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).",
            exception.Message);
        var violation = Assert.Single(exception.Violations);
        Assert.Equal(("Invoice", EntityState.Added, "acme", "globex"), (violation.Entity, violation.State, violation.Expected, violation.Actual));
        Assert.Equal(7, Assert.Single(violation.Keys).Value);
        Assert.Empty(db.AllInvoices());
    }

    [Fact]
    public void Insert_with_a_key_not_generated_yet_lists_no_keys()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { TenantId = "globex", Number = "globex-1" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Empty(Assert.Single(exception.Violations).Keys);
        Assert.Contains("\n- Added Invoice: TenantId is \"globex\"\n", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Insert_with_no_tenant_is_rejected_by_default()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = null!, Number = "acme-1" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Null(Assert.Single(exception.Violations).Actual);
    }

    [Fact]
    public void Insert_with_no_tenant_gets_the_current_tenant_under_StampOnAdd()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.StampOnAdd());
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = null!, Number = "acme-1" });

        acme.SaveChanges();

        Assert.Equal("acme", Assert.Single(db.AllInvoices()).TenantId);
    }

    [Fact]
    public void Update_of_the_current_tenants_row_is_saved()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Single().Number = "acme-1b";

        acme.SaveChanges();

        Assert.Equal("acme-1b", Assert.Single(db.AllInvoices()).Number);
    }

    [Fact]
    public void Update_of_another_tenants_row_throws()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var globex = db.Create("globex");
        var copy = new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" };
        globex.Attach(copy);
        copy.Number = "taken";

        var exception = Assert.Throws<TenantIsolationViolationException>(() => globex.SaveChanges());

        var violation = Assert.Single(exception.Violations);
        Assert.Equal((EntityState.Modified, "globex", "acme"), (violation.State, violation.Expected, violation.Actual));
        Assert.Equal("acme-1", Assert.Single(db.AllInvoices()).Number);
    }

    [Fact]
    public void Moving_a_row_to_another_tenant_throws()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Single().TenantId = "globex";

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        var violation = Assert.Single(exception.Violations);
        Assert.Equal((EntityState.Modified, "acme", "globex"), (violation.State, violation.Expected, violation.Actual));
        Assert.Contains("\n- Modified Invoice (Id: 1): TenantId changes from \"acme\" to \"globex\"\n", exception.Message, StringComparison.Ordinal);
        Assert.Equal("acme", Assert.Single(db.AllInvoices()).TenantId);
    }

    [Fact]
    public void Delete_of_the_current_tenants_row_is_saved()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Remove(acme.Invoices.Single());

        acme.SaveChanges();

        Assert.Empty(db.AllInvoices());
    }

    [Fact]
    public void Delete_of_another_tenants_row_throws()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var globex = db.Create("globex");
        globex.Invoices.Remove(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => globex.SaveChanges());

        var violation = Assert.Single(exception.Violations);
        Assert.Equal((EntityState.Deleted, "globex", "acme"), (violation.State, violation.Expected, violation.Actual));
        Assert.Single(db.AllInvoices());
    }

    [Fact]
    public void No_current_tenant_rejects_tenant_scoped_writes_by_default()
    {
        using var db = new ShopDatabase();
        using var anonymous = db.Create(tenant: null);
        anonymous.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => anonymous.SaveChanges());

        Assert.Equal(
            "TenantGuard blocked 1 write because the context has no current tenant:\n"
                + "- Added Invoice (Id: 1): TenantId is \"acme\"\n"
                + "Set the current tenant before saving, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).",
            exception.Message);
        Assert.Null(Assert.Single(exception.Violations).Expected);
    }

    [Fact]
    public void No_current_tenant_saves_and_logs_once_under_Allow()
    {
        using var db = new ShopDatabase();
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var anonymous = db.Create(tenant: null, tg => tg.WhenNoCurrentTenant(NoTenantPolicy.Allow), loggerFactory);
        anonymous.Invoices.AddRange(
            new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" },
            new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });

        anonymous.SaveChanges();

        Assert.Equal(2, db.AllInvoices().Count);
        var entry = Assert.Single(capture.TenantGuardEntries, entry => entry.EventName == "NoTenantAllowed");
        Assert.Equal((LogLevel.Information, "TenantGuard let Shop save without a current tenant (NoTenantPolicy.Allow)"), (entry.Level, entry.Message));
    }

    [Fact]
    public void Missing_CurrentTenant_is_a_configuration_error()
    {
        using var db = new ShopDatabase();
        using var shop = db.Create("acme", withCurrentTenant: false);
        shop.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = Assert.Throws<InvalidOperationException>(() => shop.SaveChanges());

        Assert.Equal(
            "TenantGuard checks the writes of Shop against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.",
            exception.Message);
    }

    [Fact]
    public void CurrentTenant_of_another_type_is_a_configuration_error()
    {
        using var db = new ShopDatabase();
        using var shop = db.Create("acme", tg => tg.CurrentTenant(_ => 42));
        shop.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = Assert.Throws<InvalidOperationException>(() => shop.SaveChanges());

        Assert.Equal(
            "CurrentTenant returned a Int32, but Invoice.TenantId is a String. Return a value of the tenant property's type.",
            exception.Message);
    }

    [Fact]
    public void Shadow_tenant_property_is_checked()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        var note = new Note { Id = 1, Text = "hello" };
        acme.Notes.Add(note);
        acme.Entry(note).Property("TenantId").CurrentValue = "globex";

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Equal(("Note", "globex"), (Assert.Single(exception.Violations).Entity, Assert.Single(exception.Violations).Actual));
    }

    [Fact]
    public void Shadow_tenant_property_is_stamped_under_StampOnAdd()
    {
        using var db = new ShopDatabase();
        using (var acme = db.Create("acme", tg => tg.StampOnAdd()))
        {
            acme.Notes.Add(new Note { Id = 1, Text = "hello" });
            acme.SaveChanges();
        }

        using var reader = db.Create("acme");
        Assert.Equal("acme", reader.Notes.Select(note => EF.Property<string>(note, "TenantId")).Single());
    }

    [Fact]
    public void Owned_types_are_skipped()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Orders.Add(new Order { Id = 1, TenantId = "acme", Shipping = new Address { Street = "1 Main St", TenantId = "globex" } });

        acme.SaveChanges();

        using var reader = db.Create("acme");
        Assert.Equal("globex", reader.Orders.Single().Shipping.TenantId);
    }

    [Fact]
    public async Task SaveChangesAsync_is_guarded_too()
    {
        using var db = new ShopDatabase();
        await using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        await Assert.ThrowsAsync<TenantIsolationViolationException>(() => acme.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(db.AllInvoices());
    }

    [Fact]
    public void Message_lists_ten_violations_and_the_total()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        for (var id = 1; id <= 12; id++)
        {
            acme.Invoices.Add(new Invoice { Id = id, TenantId = "globex", Number = $"globex-{id}" });
        }

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Equal(12, exception.Violations.Count);
        var lines = exception.Message.Split('\n');
        Assert.Equal("TenantGuard blocked 12 writes outside the current tenant \"acme\":", lines[0]);
        Assert.Equal(10, lines.Count(line => line.StartsWith("- Added Invoice", StringComparison.Ordinal)));
        Assert.Equal("- and 2 more", lines[^2]);
    }

    [Fact]
    public void Keys_are_left_out_without_IncludeKeysInErrors()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.IncludeKeysInErrors(false));
        acme.Invoices.Add(new Invoice { Id = 7, TenantId = "globex", Number = "globex-7" });

        var exception = Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Empty(Assert.Single(exception.Violations).Keys);
        Assert.Contains("\n- Added Invoice: TenantId is \"globex\"\n", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Blocked_save_is_logged_as_ViolationBlocked()
    {
        using var db = new ShopDatabase();
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var acme = db.Create("acme", loggerFactory: loggerFactory);
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        var entry = Assert.Single(capture.TenantGuardEntries, entry => entry.EventName == "ViolationBlocked");
        Assert.Equal((LogLevel.Error, "TenantGuard blocked SaveChanges on Shop; cross-tenant writes: 1"), (entry.Level, entry.Message));
    }

    [Fact]
    public void Blocked_save_stamps_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.StampOnAdd());
        var unstamped = new Invoice { Id = 1, TenantId = null!, Number = "acme-1" };
        acme.Invoices.AddRange(unstamped, new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });

        Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());

        Assert.Null(unstamped.TenantId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AutoDetectChangesEnabled_is_left_as_it_was(bool autoDetectChanges)
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        acme.SaveChanges();

        Assert.Equal(autoDetectChanges, acme.ChangeTracker.AutoDetectChangesEnabled);
    }

    [Fact]
    public void Model_without_tenant_scoped_entity_types_allocates_nothing()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder()
            .UseSqlite(connection)
            .UseTenantGuard(tg => tg.CurrentTenant(_ => "acme"))
            .Options;
        using var context = new LookupsOnly(options);
        context.Database.EnsureCreated();
        context.Countries.Add(new Country { Name = "Utopia" });

        Assert.Equal(0, AllocatedByCheck(context));
        context.SaveChanges();
    }

    [Fact]
    public void Empty_change_tracker_allocates_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");

        Assert.Equal(0, AllocatedByCheck(acme));
    }

    [Fact]
    public void Open_bypass_allocates_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });
        using var bypass = acme.TenantGuard().Bypass("allocation test");

        Assert.Equal(0, AllocatedByCheck(acme));
    }

    // What the interceptor's check allocates on a warm context, which is all SavingChanges does.
    private static long AllocatedByCheck(DbContext context)
    {
        var scope = context.GetInfrastructure().GetRequiredService<TenantGuardScope>();
        TenantWriteGuard.Check(context, scope);

        var before = GC.GetAllocatedBytesForCurrentThread();
        TenantWriteGuard.Check(context, scope);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
