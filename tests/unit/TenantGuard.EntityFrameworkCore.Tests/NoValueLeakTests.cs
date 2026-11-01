// <copyright file="NoValueLeakTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Bogus;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Support;
using static TenantGuard.EntityFrameworkCore.Tests.Models.WriteGuardModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// Exceptions and logs name tenant ids and key values and nothing else of an entity. Bogus fills
// every other value with text that can't turn up by chance, and every kind of cross-tenant write
// must leave all of it out of the exception and the log.
public sealed class NoValueLeakTests
{
    public static TheoryData<int> Seeds => [.. Enumerable.Range(1, 10)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Cross_tenant_writes_name_no_entity_values(int seed)
    {
        var faker = new Faker { Random = new Randomizer(seed) };
        var values = new List<string>();
        string Fake(Func<Faker, string> realistic)
        {
            var value = $"{realistic(faker)} {faker.Random.AlphaNumeric(12)}";
            values.Add(value);
            return value;
        }

        using var db = new ShopDatabase();
        using var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture));
        db.Seed("acme", new Invoice { Id = 1, TenantId = "acme", Number = Fake(f => f.Commerce.ProductName()) });
        var messages = new List<string>();

        // Insert of another tenant's row.
        using (var acme = db.Create("acme", loggerFactory: loggerFactory))
        {
            acme.Invoices.Add(new Invoice { Id = 2, TenantId = "globex", Number = Fake(f => f.Commerce.ProductName()) });
            messages.Add(Violation(acme));
        }

        // Update of another tenant's row.
        using (var globex = db.Create("globex", loggerFactory: loggerFactory))
        {
            var copy = new Invoice { Id = 1, TenantId = "acme", Number = values[0] };
            globex.Attach(copy);
            copy.Number = Fake(f => f.Commerce.ProductName());
            messages.Add(Violation(globex));
        }

        // Move of a row to another tenant.
        using (var acme = db.Create("acme", loggerFactory: loggerFactory))
        {
            var invoice = acme.Invoices.Single();
            invoice.TenantId = "globex";
            invoice.Number = Fake(f => f.Commerce.ProductName());
            messages.Add(Violation(acme));
        }

        // Delete of another tenant's row.
        using (var globex = db.Create("globex", loggerFactory: loggerFactory))
        {
            globex.Invoices.Remove(new Invoice { Id = 1, TenantId = "acme", Number = values[0] });
            messages.Add(Violation(globex));
        }

        // Insert of another tenant's row that owns an entity.
        using (var acme = db.Create("acme", loggerFactory: loggerFactory))
        {
            acme.Orders.Add(new Order { Id = 3, TenantId = "globex", Shipping = new Address { Street = Fake(f => f.Address.StreetAddress()), TenantId = "globex" } });
            messages.Add(Violation(acme));
        }

        messages.AddRange(capture.TenantGuardEntries.Select(entry => entry.Message));
        capture.TenantGuardEntries.Should().Contain(entry => entry.EventName == "ViolationBlocked");
        string.Join("\n", messages).Should().NotContainAny(values);
    }

    private static string Violation(Shop context)
        => FluentActions.Invoking(() => context.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which.Message;
}
