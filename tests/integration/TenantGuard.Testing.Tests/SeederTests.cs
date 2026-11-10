// <copyright file="SeederTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantGuard.Testing.Tests.Support;

namespace TenantGuard.Testing.Tests;

// The default seeder on DDD-style models: types EF Core creates through a constructor, collections
// and JSON, and a tenant property that is a foreign key to a table of tenants (docs/reference/probe.md).
public sealed class SeederTests
{
    private enum Channel
    {
        Email,
        Sms,
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Types_EF_Core_creates_through_a_constructor_are_seeded()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Ledger(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        var invoice = await acme.Set<Invoice>().SingleAsync(Token);
        (invoice.Number, invoice.Total.Currency).Should().NotBe((string.Empty, string.Empty));
    }

    [Fact]
    public async Task An_owned_value_the_constructor_or_an_initializer_sets_is_kept()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Shipping(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        var shipment = await acme.Set<Shipment>().SingleAsync(Token);
        (shipment.Destination.City.Length > 0, shipment.Destination.Fee.ToString(), shipment.Insurance.Fee.ToString()).Should().Be((true, "0 USD", "5 EUR"));
    }

    [Fact]
    public async Task An_optional_owned_constructor_parameter_gets_its_default()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Vouchers(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        (await acme.Set<Basket>().SingleAsync(Token)).Coupon.Discount.Should().BeNull();
    }

    [Fact]
    public async Task Collections_and_JSON_are_seeded_empty()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Settings(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        var profile = await acme.Set<Profile>().SingleAsync(Token);
        (profile.ExtraProperties.Count, profile.Tags.Count, profile.Channels.Count, profile.Document).Should().Be((0, 0, 0, "{}"));
    }

    [Fact]
    public async Task A_tenant_property_that_points_at_a_table_of_tenants_gets_a_row_per_tenant()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Roles(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var context = database.Create(tenant: null);
        (await context.Set<Organization>().Select(organization => organization.Id).OrderBy(id => id).ToListAsync(Token)).Should().Equal("acme", "globex");
    }

    [Fact]
    public async Task A_tenant_property_that_points_at_an_alternate_key_gets_a_row_per_tenant()
    {
        await using var database = await ProbeDatabase<Accounts>.CreateAsync(
            (options, tenant) => new Accounts(options, tenant),
            tg => tg.CurrentTenant(context => ((Accounts)context).Tenant).TenantProperty<AccountNote>(note => note.AccountName));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var context = database.Create(tenant: null);
        (await context.Set<Account>().Select(account => account.Name).OrderBy(name => name).ToListAsync(Token)).Should().Equal("acme", "globex");
    }

    [Fact]
    public async Task An_optional_composite_foreign_key_that_holds_the_tenant_points_at_the_tenants_row()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Memberships(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var acme = database.Create("acme");
        var member = await acme.Set<Member>().SingleAsync(Token);
        await using var context = database.Create(tenant: null);
        (await context.Set<Branch>().SingleAsync(branch => branch.Id == "acme", Token)).Number.Should().Be(member.BranchNumber);
    }

    [Fact]
    public async Task A_table_of_tenants_that_shares_its_key_with_another_table_gets_a_row_per_tenant_in_both()
    {
        await using var database = await DatabaseAsync((options, tenant) => new Workspaces(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.AssertAllPassed();
        await using var context = database.Create(tenant: null);
        (await context.Set<Company>().Select(company => company.Id).OrderBy(id => id).ToListAsync(Token)).Should().Equal("acme", "globex");
    }

    [Fact]
    public async Task A_table_of_tenants_the_probe_cannot_seed_names_the_tenant_property()
    {
        await using var database = await DatabaseAsync((options, tenant) => new PricedRoles(options, tenant));

        var report = await database.Probe().RunAsync(Token);

        report.Rows.Select(row => (row.Entity, row.Outcome, row.Detail)).Distinct().Should().Equal(
            ("Role", ProbeOutcome.Skipped, "Needs a seeder: its tenant property TenantId is a foreign key to Organization, which cannot be seeded: "
                + "the default seeder cannot fill the required Money property Plan, stored through ValueConverter<Money, String>, which threw: \"probe-1\" is not an amount and a currency."));
    }

    private static Task<ProbeDatabase<TContext>> DatabaseAsync<TContext>(Func<DbContextOptions, string?, TContext> create)
        where TContext : TenantContext
        => ProbeDatabase<TContext>.CreateAsync(create, tg => tg.CurrentTenant(context => ((TenantContext)context).Tenant));

    internal abstract class TenantContext(DbContextOptions options, string? tenant) : DbContext(options)
    {
        public string? Tenant { get; } = tenant;
    }

    // An invoice with no parameterless constructor, owning a total that has none either.
    private sealed class Ledger(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.OwnsOne(row => row.Total);
                invoice.HasQueryFilter(row => row.TenantId == Tenant);
            });
    }

    // A shipment owning a destination bound through its constructor and an insurance with a
    // parameterless one; each sets a fee, stored through a converter that rejects every generated value.
    private sealed class Shipping(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Shipment>(shipment =>
            {
                shipment.OwnsOne(row => row.Destination, destination => destination.Property(row => row.Fee).HasConversion(fee => fee.ToString(), text => Money.Parse(text)));
                shipment.OwnsOne(row => row.Insurance, insurance => insurance.Property(row => row.Fee).HasConversion(fee => fee.ToString(), text => Money.Parse(text)));
                shipment.Navigation(row => row.Destination).IsRequired();
                shipment.Navigation(row => row.Insurance).IsRequired();
                shipment.HasQueryFilter(row => row.TenantId == Tenant);
            });
    }

    // A basket owning a coupon bound through its constructor, whose optional discount is stored through
    // a converter that rejects every generated value.
    private sealed class Vouchers(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Basket>(basket =>
            {
                basket.OwnsOne(row => row.Coupon, coupon => coupon.Property(row => row.Discount).HasConversion(discount => discount!.ToString(), text => Money.Parse(text)));
                basket.Navigation(row => row.Coupon).IsRequired();
                basket.HasQueryFilter(row => row.TenantId == Tenant);
            });
    }

    // Seats whose tenant property is the foreign key to a workspace, whose primary key is in turn the
    // foreign key to a company: a table of tenants that shares its key.
    private sealed class Workspaces(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Workspace>().HasOne<Company>().WithOne().HasForeignKey<Workspace>(row => row.Id);
            modelBuilder.Entity<Seat>(seat =>
            {
                seat.HasOne<Workspace>().WithMany().HasForeignKey(row => row.TenantId);
                seat.HasQueryFilter(row => row.TenantId == Tenant);
            });
        }
    }

    // Members whose optional foreign key to a branch is the nullable tenant property and a required
    // branch number, pointing at the branch's alternate key.
    private sealed class Memberships(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Member>(member =>
            {
                member.HasOne<Branch>().WithMany()
                    .HasForeignKey(row => new { row.TenantId, row.BranchNumber })
                    .HasPrincipalKey(branch => new { branch.Id, branch.Number });
                member.HasQueryFilter(row => row.TenantId == Tenant);
            });
    }

    // A dictionary stored as JSON text, a primitive collection, a list of enums stored as text, and a
    // string in a jsonb column.
    private sealed class Settings(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Profile>(profile =>
            {
                profile.Property(row => row.ExtraProperties).HasConversion(
                    properties => JsonSerializer.Serialize(properties, JsonSerializerOptions.Default),
                    text => JsonSerializer.Deserialize<Dictionary<string, object?>>(text, JsonSerializerOptions.Default)!);
                profile.Property(row => row.Channels).HasConversion(
                    channels => string.Join(',', channels),
                    text => text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Channel>).ToList());
                profile.Property(row => row.Document).HasColumnType("jsonb");
                profile.HasQueryFilter(row => row.TenantId == Tenant);
            });
    }

    // Roles whose tenant property is the foreign key to the table of tenants, which has no tenant
    // property of its own.
    private class Roles(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureOrganization(modelBuilder.Entity<Organization>());
            modelBuilder.Entity<Role>(role =>
            {
                role.HasOne<Organization>().WithMany().HasForeignKey(row => row.TenantId);
                role.HasQueryFilter(row => row.TenantId == Tenant);
            });
        }

        protected virtual void ConfigureOrganization(EntityTypeBuilder<Organization> organization)
            => organization.Ignore(row => row.Plan);
    }

    // The same, with a plan on each organization that the default seeder can't fill. A context type of
    // its own, so EF Core caches its model apart from Roles'.
    private sealed class PricedRoles(DbContextOptions options, string? tenant) : Roles(options, tenant)
    {
        protected override void ConfigureOrganization(EntityTypeBuilder<Organization> organization)
            => organization.Property(row => row.Plan).HasConversion(plan => plan!.ToString(), text => Money.Parse(text)).IsRequired();
    }

    // Notes whose tenant property is the foreign key to an account's name, an alternate key.
    private sealed class Accounts(DbContextOptions options, string? tenant) : TenantContext(options, tenant)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<AccountNote>(note =>
            {
                note.HasOne<Account>().WithMany().HasForeignKey(row => row.AccountName).HasPrincipalKey(account => account.Name);
                note.HasQueryFilter(row => row.AccountName == Tenant);
            });
    }

    private sealed class Invoice(string number, string tenantId)
    {
        public int Id { get; private set; }

        public string TenantId { get; private set; } = tenantId;

        public string Number { get; private set; } = number;

        public Money Total { get; private set; } = null!;
    }

    private sealed class Money(decimal amount, string currency)
    {
        public decimal Amount { get; private set; } = amount;

        public string Currency { get; private set; } = currency;

        public static Money Parse(string text)
            => text.Split(' ') is [var amount, var currency] && decimal.TryParse(amount, CultureInfo.InvariantCulture, out var value)
                ? new Money(value, currency)
                : throw new FormatException($"\"{text}\" is not an amount and a currency.");

        public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Amount} {Currency}");
    }

    private sealed class Shipment
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Destination Destination { get; set; } = null!;

        public Insurance Insurance { get; set; } = null!;
    }

    private sealed class Destination(string city)
    {
        public string City { get; private set; } = city;

        public Money Fee { get; private set; } = new(0m, "USD");
    }

    private sealed class Insurance
    {
        public Money Fee { get; private set; } = new(5m, "EUR");
    }

    private sealed class Basket
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Coupon Coupon { get; set; } = null!;
    }

    private sealed class Coupon(string code, Money? discount)
    {
        public string Code { get; private set; } = code;

        public Money? Discount { get; private set; } = discount;
    }

    private sealed class Company
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class Workspace
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class Seat
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
    }

    private sealed class Branch
    {
        public string Id { get; set; } = string.Empty;

        public int Number { get; set; }
    }

    private sealed class Member
    {
        public int Id { get; set; }

        public string? TenantId { get; set; }

        public int BranchNumber { get; set; }
    }

    private sealed class Profile
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public Dictionary<string, object?> ExtraProperties { get; set; } = null!;

        public List<string> Tags { get; set; } = null!;

        public List<Channel> Channels { get; set; } = null!;

        public string Document { get; set; } = null!;
    }

    private sealed class Organization
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public Money? Plan { get; set; }
    }

    private sealed class Role
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }

    private sealed class Account
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class AccountNote
    {
        public int Id { get; set; }

        public string AccountName { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}
