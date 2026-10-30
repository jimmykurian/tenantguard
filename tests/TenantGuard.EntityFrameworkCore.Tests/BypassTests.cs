// <copyright file="BypassTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Models;
using TenantGuard.EntityFrameworkCore.Tests.Support;
using static TenantGuard.EntityFrameworkCore.Tests.Models.WriteGuardModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// context.TenantGuard().Bypass(reason) (SPEC 5).
public sealed class BypassTests
{
    [Fact]
    public void Bypass_lets_a_cross_tenant_write_through_and_logs_a_warning()
    {
        using var db = new ShopDatabase();
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        using var acme = db.Create("acme", loggerFactory: loggerFactory);

        using (acme.TenantGuard().Bypass("nightly aggregation across tenants"))
        {
            acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });
            acme.SaveChanges();
        }

        Assert.Equal("globex", Assert.Single(db.AllInvoices()).TenantId);
        var entry = Assert.Single(capture.TenantGuardEntries, entry => entry.EventName == "Bypass");
        Assert.Equal((LogLevel.Warning, "TenantGuard bypass: nightly aggregation across tenants (Shop)"), (entry.Level, entry.Message));
    }

    [Fact]
    public void Guard_is_back_once_the_bypass_is_disposed()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.TenantGuard().Bypass("one-off import").Dispose();
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        Assert.Throws<TenantIsolationViolationException>(() => acme.SaveChanges());
    }

    [Fact]
    public void Disposing_a_bypass_twice_ends_it_once()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");
        using var outer = acme.TenantGuard().Bypass("outer import");
        var inner = acme.TenantGuard().Bypass("inner import");
        inner.Dispose();
        inner.Dispose();
        acme.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        acme.SaveChanges();

        Assert.Single(db.AllInvoices());
    }

    [Fact]
    public void Pooled_context_comes_back_with_the_guard_on()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<Shop>()
            .UseSqlite(connection)
            .UseTenantGuard(tg => tg.CurrentTenant(context => ((Shop)context).CurrentTenant))
            .Options;
        var factory = new PooledDbContextFactory<Shop>(options);
        var first = factory.CreateDbContext();
        first.Database.EnsureCreated();
        var neverDisposed = first.TenantGuard().Bypass("left open by mistake");
        first.Dispose();

        using var second = factory.CreateDbContext();
        neverDisposed.Dispose();
        second.CurrentTenant = "acme";
        second.Invoices.Add(new Invoice { Id = 1, TenantId = "globex", Number = "globex-1" });

        Assert.Same(first, second);
        Assert.Throws<TenantIsolationViolationException>(() => second.SaveChanges());
    }

    [Fact]
    public void Bypass_leaves_the_query_filters_on()
    {
        using var db = new ShopDatabase();
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = "acme-1" });
        db.Seed("globex", new Invoice { Id = 2, TenantId = "globex", Number = "globex-1" });
        using var acme = db.Create("acme");

        using (acme.TenantGuard().Bypass("report"))
        {
            Assert.Equal("acme", Assert.Single(acme.Invoices).TenantId);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Bypass_needs_a_reason(string reason)
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");

        Assert.Throws<ArgumentException>(() => acme.TenantGuard().Bypass(reason));
    }

    [Fact]
    public void Bypass_rejects_a_null_reason()
    {
        using var db = new ShopDatabase();
        using var acme = db.Create("acme");

        Assert.Throws<ArgumentNullException>(() => acme.TenantGuard().Bypass(null!));
    }

    [Fact]
    public void TenantGuard_needs_a_context_that_uses_it()
    {
        using var context = TestModels.Create<Tgm001Models.Guarded>();

        var exception = Assert.Throws<InvalidOperationException>(() => context.TenantGuard());

        Assert.Equal("Guarded does not use TenantGuard. Add UseTenantGuard to its options.", exception.Message);
    }
}
