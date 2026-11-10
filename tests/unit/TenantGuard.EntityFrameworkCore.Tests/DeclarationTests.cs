// <copyright file="DeclarationTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Models;
using TenantGuard.EntityFrameworkCore.Tests.Support;
using static TenantGuard.EntityFrameworkCore.Tests.Models.DeclarationModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// Cross-tenant data declared on purpose: Unfiltered, CrossTenant, CrossTenantWhenNoTenant,
// NullTenantIsHost, Suppress, and a tenant registry (docs/reference/options.md#cross-tenant-data).
public sealed class DeclarationTests : IDisposable
{
    private const string Why = "shared across tenants on purpose";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public DeclarationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Types_without_a_filter_are_TGM001_until_declared_unfiltered()
    {
        TestModels.Validate<Members>().Findings.Select(finding => (finding.RuleId, finding.Entity))
            .Should().Equal(("TGM001", "Membership"), ("TGM001", "OutboxMessage"));

        TestModels.Validate<Members>(tg => tg.Unfiltered<Membership>(Why).Unfiltered<OutboxMessage>(Why)).Findings.Should().BeEmpty();
    }

    [Fact]
    public void An_unfiltered_type_keeps_the_rules_about_its_column_and_indexes()
        => TestModels.ValidateAll<Members>(tg => tg.Unfiltered<Membership>(Why).Unfiltered<OutboxMessage>(Why))
            .Findings.Should().ContainSingle().Which.Should().Match<ModelFinding>(finding => finding.RuleId == "TGM009" && finding.Entity == "OutboxMessage");

    [Fact]
    public void An_unfiltered_types_rows_are_not_checked_and_the_others_still_are()
    {
        using var acme = Create<Members>(_connection, "acme", tg => tg.Unfiltered<Membership>(Why).Unfiltered<OutboxMessage>(Why));
        acme.Database.EnsureCreated();

        acme.Memberships.Add(new Membership { TenantId = "globex", UserId = "u1" });
        acme.SaveChanges();
        acme.Invoices.Add(new Invoice { TenantId = "globex" });

        FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>()
            .Which.Violations.Should().ContainSingle().Which.Entity.Should().Be("Invoice");
    }

    [Theory]
    [InlineData("Country", "Unfiltered<Country>() names a type with no tenant property. Declare a tenant-free type with Shared<Country>().")]
    [InlineData("Address", "Unfiltered<Address>() names a derived or owned type. Declare the root of its hierarchy, or its owner, Order.")]
    [InlineData("Contract", "Unfiltered<Contract>() names a derived or owned type. Declare the root of its hierarchy, or its owner, Document.")]
    public void Declaring_a_type_unfiltered_that_cannot_be_throws_when_the_model_is_built(string type, string message)
    {
        using var context = Create<Catalog>(_connection, "acme", tg => _ = type switch
        {
            "Country" => tg.Unfiltered<Country>(Why),
            "Address" => tg.Unfiltered<Address>(Why),
            _ => tg.Unfiltered<Contract>(Why),
        });

        FluentActions.Invoking(() => context.Model).Should().Throw<InvalidOperationException>().WithMessage(message);
    }

    [Fact]
    public void A_declaration_for_a_type_the_model_does_not_map_is_ignored()
    {
        using var context = Create<Registry>(_connection, "acme", tg => tg.Unfiltered<Membership>(Why).TenantProperty<Club>(club => club.Id));

        context.Model.Should().NotBeNull();
    }

    [Fact]
    public void A_cross_tenant_context_runs_no_rule_and_saves_any_tenants_rows()
    {
        using var context = Create<Members>(_connection, tenant: null, tg => tg.CrossTenant("reports across tenants"));
        context.Database.EnsureCreated();

        context.Invoices.Add(new Invoice { TenantId = "acme" });
        context.Memberships.Add(new Membership { TenantId = "globex", UserId = "u1" });
        context.SaveChanges();

        TestModels.Validate<Members>(tg => tg.CrossTenant("reports across tenants")).Findings.Should().BeEmpty();
    }

    [Fact]
    public void Null_tenant_host_rows_are_TGM005_until_declared()
    {
        TestModels.Validate<HostShop>().Findings.Should().ContainSingle().Which.RuleId.Should().Be("TGM005");

        TestModels.Validate<HostShop>(tg => tg.NullTenantIsHost()).Findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "acme", false)]
    [InlineData("acme", null, false)]
    [InlineData("acme", "acme", true)]
    public void The_host_writes_host_rows_and_each_tenant_its_own(string? current, string? row, bool written)
    {
        using (var setup = Create<HostShop>(_connection, null, tg => tg.NullTenantIsHost()))
        {
            setup.Database.EnsureCreated();
        }

        using var context = Create<HostShop>(_connection, current, tg => tg.NullTenantIsHost());
        context.Invoices.Add(new HostInvoice { TenantId = row });

        var save = FluentActions.Invoking(() => context.SaveChanges());

        if (written)
        {
            save.Should().NotThrow();
        }
        else
        {
            save.Should().ThrowExactly<TenantIsolationViolationException>();
        }
    }

    [Fact]
    public void The_host_cannot_move_its_row_to_a_tenant()
    {
        using var host = Create<HostShop>(_connection, null, tg => tg.NullTenantIsHost());
        host.Database.EnsureCreated();
        var invoice = new HostInvoice();
        host.Invoices.Add(invoice);
        host.SaveChanges();

        invoice.TenantId = "acme";

        FluentActions.Invoking(() => host.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>();
    }

    [Fact]
    public void Without_the_host_declaration_no_tenant_follows_WhenNoCurrentTenant()
    {
        using var host = Create<HostShop>(_connection, null, _ => { });
        host.Database.EnsureCreated();
        host.Invoices.Add(new HostInvoice());

        FluentActions.Invoking(() => host.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>();
    }

    [Fact]
    public void Suppress_moves_a_finding_to_Suppressed()
    {
        var report = TestModels.ValidateAll<Members>(tg => tg
            .Unfiltered<Membership>(Why)
            .Unfiltered<OutboxMessage>(Why)
            .Suppress<OutboxMessage>("TGM009", "read by its key only"));

        report.Findings.Should().BeEmpty();
        report.Suppressed.Should().ContainSingle().Which.Should().Match<ModelFinding>(finding => finding.RuleId == "TGM009" && finding.Entity == "OutboxMessage");
    }

    [Fact]
    public void A_global_suppression_covers_every_type()
    {
        var report = TestModels.ValidateAll<HostShop>(tg => tg.Suppress("TGM005", "rows without a tenant are meant"));

        report.Findings.Should().BeEmpty();
        report.Suppressed.Should().ContainSingle().Which.RuleId.Should().Be("TGM005");
    }

    [Fact]
    public void A_suppressed_error_does_not_throw()
    {
        using var context = Create<Members>(_connection, "acme", tg => tg
            .Unfiltered<Membership>(Why)
            .Suppress<OutboxMessage>("TGM003", "unrelated")
            .Unfiltered<OutboxMessage>(Why));

        context.Model.Should().NotBeNull();
    }

    [Theory]
    [InlineData("TGM015", "Unknown rule 'TGM015'. Pass a rule ID from TGM001 to TGM014. (Parameter 'ruleId')")]
    [InlineData("TGM010", "TGM010 is an error, so it is suppressed one entity type at a time: Suppress<TEntity>(\"TGM010\", reason). (Parameter 'ruleId')")]
    public void Suppress_takes_only_a_known_warning_rule(string ruleId, string message)
        => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().Suppress(ruleId, Why))
            .Should().ThrowExactly<ArgumentException>().WithMessage(message);

    [Theory]
    [InlineData("TGM001", "Declare Invoice unfiltered with Unfiltered<Invoice>(reason) rather than suppressing TGM001. (Parameter 'ruleId')")]
    [InlineData("TGM002", "Declare Invoice shared with Shared<Invoice>() rather than suppressing TGM002. (Parameter 'ruleId')")]
    [InlineData("TGM015", "Unknown rule 'TGM015'. Pass a rule ID from TGM001 to TGM014. (Parameter 'ruleId')")]
    public void Suppress_for_a_type_points_TGM001_and_TGM002_to_their_declarations(string ruleId, string message)
        => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().Suppress<Invoice>(ruleId, Why))
            .Should().ThrowExactly<ArgumentException>().WithMessage(message);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Every_declaration_needs_a_reason(string? reason)
    {
        var builder = new TenantGuardOptionsBuilder();
        var declarations = new Action[]
        {
            () => builder.Unfiltered<Invoice>(reason!),
            () => builder.CrossTenant(reason!),
            () => builder.CrossTenantWhenNoTenant(reason!),
            () => builder.Suppress("TGM009", reason!),
            () => builder.Suppress<Invoice>("TGM009", reason!),
        };

        foreach (var declaration in declarations)
        {
            if (reason is null)
            {
                declaration.Should().ThrowExactly<ArgumentNullException>();
            }
            else
            {
                declaration.Should().ThrowExactly<ArgumentException>();
            }
        }
    }

    [Fact]
    public void A_tenant_registry_is_tenant_scoped_by_its_key()
    {
        TestModels.Validate<Registry>(tg => tg.RequireAllEntitiesScoped()).Findings.Should().ContainSingle().Which.RuleId.Should().Be("TGM002");

        TestModels.Validate<Registry>(tg => tg.RequireAllEntitiesScoped().TenantProperty<Club>(club => club.Id)).Findings.Should().BeEmpty();
    }

    [Fact]
    public void The_no_tenant_model_of_a_model_per_tenant_is_cross_tenant()
    {
        using var host = Create<OpenLedger>(_connection, tenant: null, NoTenantCrossTenant, typeof(TenantKeyFactory));
        host.Database.EnsureCreated();
        using var acme = Create<OpenLedger>(_connection, "acme", NoTenantCrossTenant, typeof(TenantKeyFactory));

        host.Invoices.Add(new Invoice { TenantId = "acme" });
        host.Invoices.Add(new Invoice { TenantId = "globex" });
        host.SaveChanges();

        acme.Model.Should().NotBeSameAs(host.Model);
        acme.Invoices.Select(invoice => invoice.TenantId).ToList().Should().Equal("acme");
    }

    [Fact]
    public void With_EF_Cores_own_model_cache_the_no_tenant_model_is_checked()
    {
        using var host = Create<OpenLedger>(_connection, tenant: null, tg => tg.CrossTenantWhenNoTenant("background work"));

        FluentActions.Invoking(() => host.Model).Should().Throw<TenantGuardModelException>()
            .Which.Report.Errors.Should().ContainSingle().Which.RuleId.Should().Be("TGM001");
    }

    [Fact]
    public void A_tenants_save_through_the_no_tenant_model_is_blocked()
    {
        using var host = Create<OpenLedger>(_connection, tenant: null, NoTenantCrossTenant, typeof(SharedKeyFactory));
        host.Database.EnsureCreated();
        using var acme = Create<OpenLedger>(_connection, "acme", NoTenantCrossTenant, typeof(SharedKeyFactory));

        acme.Model.Should().BeSameAs(host.Model);
        acme.Invoices.Add(new Invoice { TenantId = "acme" });
        var exception = FluentActions.Invoking(() => acme.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().BeEmpty();
        exception.Message.Should().Be(
            "TenantGuard blocked a save to OpenLedger: its model was built with no tenant and declared cross-tenant, so its query filters read every tenant's rows, "
            + "but the current tenant is \"acme\". Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.");
    }

    [Fact]
    public void Each_declaration_is_logged_with_its_reason()
    {
        var capture = new LogCapture();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(capture).SetMinimumLevel(LogLevel.Debug));
        using var members = Create<Members>(
            _connection,
            "acme",
            tg => tg.Unfiltered<Membership>("maps users to tenants").Unfiltered<OutboxMessage>(Why).Suppress<OutboxMessage>("TGM009", "read by key"),
            loggerFactory: loggerFactory);
        using var reports = Create<Members>(_connection, null, tg => tg.CrossTenant("reports across tenants"), loggerFactory: loggerFactory);

        _ = members.Model;
        _ = reports.Model;

        capture.TenantGuardEntries.Where(entry => entry.EventName is "UnfilteredEntity" or "CrossTenantModel" or "FindingSuppressed")
            .Select(entry => (entry.EventName, entry.Level, entry.Message)).Should().Equal(
                ("UnfilteredEntity", LogLevel.Information, "TenantGuard left Membership unfiltered, as declared: maps users to tenants"),
                ("UnfilteredEntity", LogLevel.Information, $"TenantGuard left OutboxMessage unfiltered, as declared: {Why}"),
                ("FindingSuppressed", LogLevel.Debug, "TGM009 suppressed for OutboxMessage: read by key"),
                ("CrossTenantModel", LogLevel.Information, "TenantGuard skipped the rules for Members, declared cross-tenant: reports across tenants"));
    }

    [Fact]
    public void The_declarations_take_part_in_the_service_provider_key()
    {
        var plain = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Options).Info;
        var declared = new Func<TenantGuardOptionsBuilder, TenantGuardOptionsBuilder>[]
        {
            tg => tg.Unfiltered<Invoice>(Why),
            tg => tg.CrossTenant(Why),
            tg => tg.CrossTenantWhenNoTenant(Why),
            tg => tg.NullTenantIsHost(),
            tg => tg.Suppress("TGM009", Why),
            tg => tg.Suppress<Invoice>("TGM009", Why),
        };

        foreach (var declare in declared)
        {
            plain.ShouldUseSameServiceProvider(new TenantGuardOptionsExtension(declare(new TenantGuardOptionsBuilder()).Options).Info)
                .Should().BeFalse();
        }
    }

    private static void NoTenantCrossTenant(TenantGuardOptionsBuilder tg)
        => tg.AllowCapturedValues().CrossTenantWhenNoTenant("background work across tenants");
}
