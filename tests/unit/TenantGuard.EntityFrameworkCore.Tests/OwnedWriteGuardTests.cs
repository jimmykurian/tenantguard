// <copyright file="OwnedWriteGuardTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.EntityFrameworkCore.Tests.Support;
using static TenantGuard.EntityFrameworkCore.Tests.Models.WriteGuardModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// Writes to an owned entity, checked through its owner (ADR 0014): Order owns an Address (OwnsOne),
// which owns a GeoPoint, and OrderLines (OwnsMany).
public sealed class OwnedWriteGuardTests
{
    [Fact]
    public void Changing_an_owned_reference_of_another_tenants_row_is_rejected()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        var order = globex.Orders.IgnoreQueryFilters().Single();
        order.Shipping.Street = "9 Elm St";

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().Be("TenantGuard blocked 1 write outside the current tenant \"globex\":\n"
            + "- Modified Address (OrderId: 1): owned by Order, whose TenantId is \"acme\"\n"
            + "Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).");
        exception.Violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TenantViolation("Address", EntityState.Modified, Keys(("OrderId", 1)), "globex", "acme") { Owner = "Order" }, options => options.ComparingByMembers<TenantViolation>());
        StoredOrder(db).Shipping.Street.Should().Be("1 Main St");
    }

    [Fact]
    public void Changing_a_nested_owned_reference_of_another_tenants_row_is_rejected()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        globex.Orders.IgnoreQueryFilters().Single().Shipping.Geo.Latitude = 10;

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TenantViolation("GeoPoint", EntityState.Modified, Keys(("AddressOrderId", 1)), "globex", "acme") { Owner = "Order" }, options => options.ComparingByMembers<TenantViolation>());
        StoredOrder(db).Shipping.Geo.Latitude.Should().Be(1);
    }

    [Fact]
    public void Adding_an_owned_line_to_another_tenants_row_is_rejected()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        globex.Orders.IgnoreQueryFilters().Single().Lines.Add(new OrderLine { Id = 3, Sku = "globex-sku" });

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().Contain("\n- Added OrderLine (OrderId: 1, Id: 3): owned by Order, whose TenantId is \"acme\"\n");
        StoredOrder(db).Lines.Should().HaveCount(2);
    }

    [Fact]
    public void Changing_an_owned_line_of_another_tenants_row_is_rejected()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        globex.Orders.IgnoreQueryFilters().Single().Lines.Single(line => line.Id == 1).Sku = "globex-sku";

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TenantViolation("OrderLine", EntityState.Modified, Keys(("OrderId", 1), ("Id", 1)), "globex", "acme") { Owner = "Order" }, options => options.ComparingByMembers<TenantViolation>());
        StoredOrder(db).Lines.Select(line => line.Sku).Should().BeEquivalentTo("acme-1", "acme-2");
    }

    [Fact]
    public void Deleting_an_owned_line_of_another_tenants_row_is_rejected()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        var order = globex.Orders.IgnoreQueryFilters().Single();
        order.Lines.Remove(order.Lines.Single(line => line.Id == 2));

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TenantViolation("OrderLine", EntityState.Deleted, Keys(("OrderId", 1), ("Id", 2)), "globex", "acme") { Owner = "Order" }, options => options.ComparingByMembers<TenantViolation>());
        StoredOrder(db).Lines.Should().HaveCount(2);
    }

    [Fact]
    public void An_attached_row_of_another_tenant_is_checked_through_its_owner()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        var order = new Order { Id = 1, TenantId = "acme", Shipping = new Address { Street = "1 Main St" } };
        globex.Attach(order);
        order.Shipping.Street = "9 Elm St";

        FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>()
            .Which.Violations.Should().ContainSingle().Which.Owner.Should().Be("Order");
        StoredOrder(db).Shipping.Street.Should().Be("1 Main St");
    }

    [Fact]
    public void Owned_writes_of_the_current_tenants_row_are_saved()
    {
        using var db = SeededDatabase();
        using (var acme = db.Create("acme"))
        {
            var order = acme.Orders.Single();
            order.Shipping.Street = "9 Elm St";
            order.Shipping.Geo.Latitude = 10;
            order.Lines.Single(line => line.Id == 1).Sku = "acme-1b";
            order.Lines.Remove(order.Lines.Single(line => line.Id == 2));
            order.Lines.Add(new OrderLine { Id = 3, Sku = "acme-3" });

            acme.SaveChanges();
        }

        var stored = StoredOrder(db);
        (stored.Shipping.Street, stored.Shipping.Geo.Latitude).Should().Be(("9 Elm St", 10));
        stored.Lines.Select(line => line.Sku).Should().BeEquivalentTo("acme-1b", "acme-3");
    }

    [Fact]
    public void A_new_owner_with_owned_parts_is_stamped_under_StampOnAdd()
    {
        using var db = new ShopDatabase();
        using (var acme = db.Create("acme", tg => tg.StampOnAdd()))
        {
            acme.Orders.Add(new Order { Id = 1, TenantId = null!, Shipping = new Address { Street = "1 Main St" }, Lines = [new OrderLine { Id = 1, Sku = "acme-1" }] });
            acme.SaveChanges();
        }

        var stored = StoredOrder(db);
        (stored.TenantId, stored.Lines.Count).Should().Be(("acme", 1));
    }

    [Fact]
    public void An_owner_written_with_its_owned_parts_is_reported_once()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex");
        var order = globex.Orders.IgnoreQueryFilters().Single();
        order.TenantId = "globex";
        order.Shipping.Street = "9 Elm St";
        order.Lines.Add(new OrderLine { Id = 3, Sku = "globex-sku" });

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TenantViolation("Order", EntityState.Modified, Keys(("Id", 1)), "globex", "acme"), options => options.ComparingByMembers<TenantViolation>());
    }

    [Fact]
    public void Owned_writes_with_no_current_tenant_are_rejected()
    {
        using var db = SeededDatabase();
        using var none = db.Create(tenant: null);
        none.Orders.IgnoreQueryFilters().Single().Shipping.Street = "9 Elm St";

        var exception = FluentActions.Invoking(() => none.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().StartWith("TenantGuard blocked 1 write because the context has no current tenant:\n"
            + "- Modified Address (OrderId: 1): owned by Order, whose TenantId is \"acme\"\n");
    }

    [Fact]
    public void Owned_writes_follow_NoTenantPolicy_Allow()
    {
        using var db = SeededDatabase();
        using (var none = db.Create(tenant: null, tg => tg.WhenNoCurrentTenant(NoTenantPolicy.Allow)))
        {
            none.Orders.IgnoreQueryFilters().Single().Shipping.Street = "9 Elm St";
            none.SaveChanges();
        }

        StoredOrder(db).Shipping.Street.Should().Be("9 Elm St");
    }

    [Fact]
    public void Owned_writes_inside_a_bypass_are_saved()
    {
        using var db = SeededDatabase();
        using (var globex = db.Create("globex"))
        using (globex.TenantGuard().Bypass("support fixes an address"))
        {
            globex.Orders.IgnoreQueryFilters().Single().Shipping.Street = "9 Elm St";
            globex.SaveChanges();
        }

        StoredOrder(db).Shipping.Street.Should().Be("9 Elm St");
    }

    [Fact]
    public void Owned_writes_of_an_unfiltered_owner_are_not_checked()
    {
        using var db = SeededDatabase();
        using (var globex = db.Create("globex", tg => tg.Unfiltered<Order>("orders are dispatched by one worker")))
        {
            globex.Orders.IgnoreQueryFilters().Single().Shipping.Street = "9 Elm St";
            globex.SaveChanges();
        }

        StoredOrder(db).Shipping.Street.Should().Be("9 Elm St");
    }

    [Fact]
    public void Keys_are_left_out_without_IncludeKeysInErrors()
    {
        using var db = SeededDatabase();
        using var globex = db.Create("globex", tg => tg.IncludeKeysInErrors(false));
        globex.Orders.IgnoreQueryFilters().Single().Shipping.Street = "9 Elm St";

        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Message.Should().Contain("\n- Modified Address: owned by Order, whose TenantId is \"acme\"\n");
    }

    [Fact]
    public void An_owned_types_own_tenant_property_is_ignored()
    {
        using var db = new ShopDatabase();
        using (var acme = db.Create("acme"))
        {
            acme.Orders.Add(new Order { Id = 1, TenantId = "acme", Shipping = new Address { Street = "1 Main St", TenantId = "globex" } });
            acme.SaveChanges();
        }

        StoredOrder(db).Shipping.TenantId.Should().Be("globex");
    }

    // acme's Order 1, shipped to 1 Main St at latitude 1, with lines 1 and 2.
    private static ShopDatabase SeededDatabase()
    {
        var db = new ShopDatabase();
        using var acme = db.Create("acme");
        acme.Orders.Add(new Order
        {
            Id = 1,
            TenantId = "acme",
            Shipping = new Address { Street = "1 Main St", Geo = new GeoPoint { Latitude = 1 } },
            Lines = [new OrderLine { Id = 1, Sku = "acme-1" }, new OrderLine { Id = 2, Sku = "acme-2" }],
        });
        acme.SaveChanges();
        return db;
    }

    private static Order StoredOrder(ShopDatabase db)
    {
        using var reader = db.Create(tenant: null);
        return reader.Orders.IgnoreQueryFilters().AsNoTracking().Single();
    }

    private static Dictionary<string, object?> Keys(params (string Name, object? Value)[] keys)
        => keys.ToDictionary(key => key.Name, key => key.Value, StringComparer.Ordinal);
}
