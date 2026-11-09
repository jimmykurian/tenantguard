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
