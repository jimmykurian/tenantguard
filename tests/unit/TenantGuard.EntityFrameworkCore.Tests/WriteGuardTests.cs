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

        db.AllInvoices().Should().ContainSingle().Subject.TenantId.Should().Be("acme");
    }

    [Fact]
    public void Insert_for_another_tenant_throws_before_the_database_sees_it()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 7, TenantId = "globex", Number = "globex-7" });

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().Be("TenantGuard blocked 1 write outside the current tenant \"acme\":\n"
                + "- Added Invoice (Id: 7): TenantId is \"globex\"\n"
                + "Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).");
        var violation = exception.Violations.Should().ContainSingle().Subject;
        (violation.Entity, violation.State, violation.Expected, violation.Actual).Should().Be(("Invoice", EntityState.Added, "acme", "globex"));
        violation.Keys.Should().ContainSingle().Subject.Value.Should().Be(7);
        db.AllInvoices().Should().BeEmpty();
    }

    [Fact]
    public void Insert_with_a_key_not_generated_yet_lists_no_keys()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { TenantId = "globex", Number = "globex-1" });

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Subject.Keys.Should().BeEmpty();
        exception.Message.Should().Contain("\n- Added Invoice: TenantId is \"globex\"\n");
    }

    [Fact]
    public void Insert_with_no_tenant_is_rejected_by_default()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = null!, Number = "acme-1" });

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Subject.Actual.Should().BeNull();
    }

    [Fact]
    public void Insert_with_no_tenant_gets_the_current_tenant_under_StampOnAdd()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.StampOnAdd());
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = null!, Number = "acme-1" });

        acme.SaveChanges();

        db.AllInvoices().Should().ContainSingle().Subject.TenantId.Should().Be("acme");
    }

    [Fact]
    public void Update_of_the_current_tenants_row_is_saved()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Single().Number = "acme-1b";

        acme.SaveChanges();

        db.AllInvoices().Should().ContainSingle().Subject.Number.Should().Be("acme-1b");
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

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        var violation = exception.Violations.Should().ContainSingle().Subject;
        (violation.State, violation.Expected, violation.Actual).Should().Be((EntityState.Modified, "globex", "acme"));
        db.AllInvoices().Should().ContainSingle().Subject.Number.Should().Be("acme-1");
    }

    [Fact]
    public void Moving_a_row_to_another_tenant_throws()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Single().TenantId = "globex";

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        var violation = exception.Violations.Should().ContainSingle().Subject;
        (violation.State, violation.Expected, violation.Actual).Should().Be((EntityState.Modified, "acme", "globex"));
        exception.Message.Should().Contain("\n- Modified Invoice (Id: 1): TenantId changes from \"acme\" to \"globex\"\n");
        db.AllInvoices().Should().ContainSingle().Subject.TenantId.Should().Be("acme");
    }

    [Fact]
    public void Delete_of_the_current_tenants_row_is_saved()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var acme = db.Create("acme");
        acme.Invoices.Remove(acme.Invoices.Single());

        acme.SaveChanges();

        db.AllInvoices().Should().BeEmpty();
    }

    [Fact]
    public void Delete_of_another_tenants_row_throws()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        using var globex = db.Create("globex");
        globex.Invoices.Remove(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        var violation = exception.Violations.Should().ContainSingle().Subject;
        (violation.State, violation.Expected, violation.Actual).Should().Be((EntityState.Deleted, "globex", "acme"));
        db.AllInvoices().Should().ContainSingle();
    }

    [Fact]
    public void No_current_tenant_rejects_tenant_scoped_writes_by_default()
    {
        using var db = new ShopDatabase();
        using var anonymous = db.Create(tenant: null);
        anonymous.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = FluentActions.Invoking(() => anonymous.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().Be("TenantGuard blocked 1 write because the context has no current tenant:\n"
                + "- Added Invoice (Id: 1): TenantId is \"acme\"\n"
                + "Set the current tenant before saving, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).");
        exception.Violations.Should().ContainSingle().Subject.Expected.Should().BeNull();
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

        db.AllInvoices().Count.Should().Be(2);
        var entry = capture.TenantGuardEntries.Should().ContainSingle(entry => entry.EventName == "NoTenantAllowed").Subject;
        (entry.Level, entry.Message).Should().Be((LogLevel.Information, "TenantGuard let Shop save without a current tenant (NoTenantPolicy.Allow)"));
    }

    [Fact]
    public void Missing_CurrentTenant_is_a_configuration_error()
    {
        using var db = new ShopDatabase();
        using var shop = db.Create("acme", withCurrentTenant: false);
        shop.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = FluentActions.Invoking(() => shop.SaveChanges()).Should().ThrowExactly<InvalidOperationException>().Which;

        exception.Message.Should().Be("TenantGuard checks the writes of Shop against its current tenant, but no CurrentTenant is configured. Add CurrentTenant(ctx => ...) to UseTenantGuard.");
    }

    [Fact]
    public void CurrentTenant_of_another_type_is_a_configuration_error()
    {
        using var db = new ShopDatabase();
        using var shop = db.Create("acme", tg => tg.CurrentTenant(_ => 42));
        shop.Invoices.Add(new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });

        var exception = FluentActions.Invoking(() => shop.SaveChanges()).Should().ThrowExactly<InvalidOperationException>().Which;

        exception.Message.Should().Be("CurrentTenant returned a Int32, but Invoice.TenantId is a String. Return a value of the tenant property's type.");
    }

    [Fact]
    public void Shadow_tenant_property_is_checked()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        var note = new Note { Id = 1, Text = "hello" };
        acme.Notes.Add(note);
        acme.Entry(note).Property("TenantId").CurrentValue = "globex";

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        (exception.Violations.Should().ContainSingle().Subject.Entity, exception.Violations.Should().ContainSingle().Subject.Actual).Should().Be(("Note", "globex"));
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
        reader.Notes.Select(note => EF.Property<string>(note, "TenantId")).Single().Should().Be("acme");
    }

    [Fact]
    public async Task SaveChangesAsync_is_guarded_too()
    {
        using var db = new ShopDatabase();
        await using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        await FluentActions.Awaiting(() => acme.SaveChangesAsync(TestContext.Current.CancellationToken)).Should().ThrowExactlyAsync<TenantIsolationViolationException>();

        db.AllInvoices().Should().BeEmpty();
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

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Count.Should().Be(12);
        var lines = exception.Message.Split('\n');
        lines[0].Should().Be("TenantGuard blocked 12 writes outside the current tenant \"acme\":");
        lines.Count(line => line.StartsWith("- Added Invoice", StringComparison.Ordinal)).Should().Be(10);
        lines[^2].Should().Be("- and 2 more");
    }

    [Fact]
    public void Keys_are_left_out_without_IncludeKeysInErrors()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.IncludeKeysInErrors(false));
        acme.Invoices.Add(new Invoice { Id = 7, TenantId = "globex", Number = "globex-7" });

        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Subject.Keys.Should().BeEmpty();
        exception.Message.Should().Contain("\n- Added Invoice: TenantId is \"globex\"\n");
    }

    [Fact]
    public void Blocked_save_is_logged_as_ViolationBlocked()
    {
        using var db = new ShopDatabase();
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var acme = db.Create("acme", loggerFactory: loggerFactory);
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>();

        var entry = capture.TenantGuardEntries.Should().ContainSingle(entry => entry.EventName == "ViolationBlocked").Subject;
        (entry.Level, entry.Message).Should().Be((LogLevel.Error, "TenantGuard blocked SaveChanges on Shop; cross-tenant writes: 1"));
    }

    [Fact]
    public void Blocked_save_stamps_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme", tg => tg.StampOnAdd());
        var unstamped = new Invoice { Id = 1, TenantId = null!, Number = "acme-1" };
        acme.Invoices.AddRange(unstamped, new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });

        FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>();

        unstamped.TenantId.Should().BeNull();
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

        acme.ChangeTracker.AutoDetectChangesEnabled.Should().Be(autoDetectChanges);
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

        AllocatedByCheck(context).Should().Be(0);
        context.SaveChanges();
    }

    [Fact]
    public void Empty_change_tracker_allocates_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");

        AllocatedByCheck(acme).Should().Be(0);
    }

    [Fact]
    public void Open_bypass_allocates_nothing()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });
        using var bypass = acme.TenantGuard().Bypass("allocation test");

        AllocatedByCheck(acme).Should().Be(0);
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
