// <copyright file="ForeignKeyRuleTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// TGM015 against its positive and negative models, with the exact documented messages.
public sealed class ForeignKeyRuleTests
{
    private const string Why = "the foreign keys change in a later migration";

    [Fact]
    public void Foreign_key_without_the_tenant_property_is_TGM015()
        => TestModels.ValidateAll<Tgm015Models.SingleColumn>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM015",
            Severity.Warning,
            "Invoice",
            "TenantId",
            "Invoice: the foreign key (CustomerId) to Customer doesn't include 'TenantId', so the database accepts a row that points at another tenant's Customer. Add it to the foreign key: HasForeignKey(e => new { e.TenantId, e.CustomerId }).HasPrincipalKey(p => new { p.TenantId, p.Id })."));

    [Fact]
    public void Optional_foreign_key_is_reported_too()
        => Reported<Tgm015Models.Optional>().Should().ContainSingle().Which.Should().StartWith("Invoice: the foreign key (CustomerId) to Customer doesn't include 'TenantId'");

    [Fact]
    public void Foreign_key_with_the_tenant_pair_passes()
        => TestModels.ValidateAll<Tgm015Models.Composite>().Findings.Should().BeEmpty();

    [Fact]
    public void Fix_replaces_a_pair_that_already_holds_the_principals_tenant_property()
        => Reported<Tgm015Models.OtherTenantColumn>().Should().Equal(
            "Invoice: the foreign key (CustomerTenantId, CustomerId) to Customer doesn't include 'TenantId', so the database accepts a row that points at another tenant's Customer. Add it to the foreign key: HasForeignKey(e => new { e.TenantId, e.CustomerId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).");

    [Fact]
    public void Tenant_property_paired_with_another_principal_property_gets_the_message_without_code()
        => Reported<Tgm015Models.Mispaired>().Should().Equal(
            "Invoice: the foreign key (TenantId) to Customer pairs 'TenantId' with 'Code', not with Customer's tenant property 'TenantId', so the database accepts a row that points at another tenant's Customer. Pair 'TenantId' with 'TenantId' in the foreign key, and keep 'Code' paired with a property of its own.");

    [Fact]
    public void Each_foreign_key_is_reported_in_the_order_of_its_columns()
        => Reported<Tgm015Models.TwoForeignKeys>().Should().SatisfyRespectively(
            first => first.Should().StartWith("Invoice: the foreign key (BillToId) to Customer"),
            second => second.Should().StartWith("Invoice: the foreign key (CustomerId) to Customer"));

    [Fact]
    public void Self_reference_is_reported()
        => Reported<Tgm015Models.SelfReference>().Should().Equal(
            "Category: the foreign key (ParentId) to Category doesn't include 'TenantId', so the database accepts a row that points at another tenant's Category. Add it to the foreign key: HasForeignKey(e => new { e.TenantId, e.ParentId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).");

    [Fact]
    public void Foreign_key_declared_on_a_derived_type_is_reported_for_that_type()
        => Reported<Tgm015Models.OnDerived>().Should().ContainSingle().Which.Should().StartWith("CreditNote: the foreign key (RefundedCustomerId) to Customer doesn't include 'TenantId'");

    [Fact]
    public void Shadow_tenant_properties_are_reported_by_name()
        => Reported<Tgm015Models.ShadowTenant>().Should().Equal(
            "ProjectTask: the foreign key (ProjectId) to Project doesn't include 'TenantId', so the database accepts a row that points at another tenant's Project. Add it to the foreign key: HasForeignKey(e => new { e.TenantId, e.ProjectId }).HasPrincipalKey(p => new { p.TenantId, p.Id }).");

    [Fact]
    public void Shadow_tenant_properties_paired_by_name_pass()
        => TestModels.ValidateAll<Tgm015Models.ShadowTenantComposite>().Findings.Should().BeEmpty();

    [Theory]
    [InlineData(typeof(Tgm015Models.SharedPrincipal))]
    [InlineData(typeof(Tgm015Models.TableOfTenants))]
    [InlineData(typeof(Tgm015Models.UnscopedDependent))]
    [InlineData(typeof(Tgm015Models.OwnedDependent))]
    [InlineData(typeof(Tgm015Models.ImplicitJoin))]
    [InlineData(typeof(Tgm015Models.PrincipalIsView))]
    [InlineData(typeof(Tgm015Models.DependentIsView))]
    public void Foreign_keys_the_rule_leaves_out_pass(Type context)
        => Reported(context).Should().BeEmpty();

    [Fact]
    public void Join_entity_type_with_a_tenant_property_has_its_foreign_keys_checked()
        => Reported<Tgm015Models.ExplicitJoin>().Should().SatisfyRespectively(
            first => first.Should().StartWith("CustomerTag: the foreign key (CustomerId) to Customer doesn't include 'TenantId'"),
            second => second.Should().StartWith("CustomerTag: the foreign key (TagId) to Tag doesn't include 'TenantId'"));

    [Fact]
    public void Principal_declared_unfiltered_is_still_reported()
        => Reported<Tgm015Models.SingleColumn>(tg => tg.Unfiltered<Tgm015Models.Customer>("read by the billing job for every tenant")).Should().ContainSingle();

    [Fact]
    public void Non_relational_model_is_skipped()
        => TestModels.ValidateWithoutProvider(modelBuilder =>
        {
            modelBuilder.Entity<Tgm015Models.Customer>(customer =>
            {
                customer.HasQueryFilter(row => row.TenantId == "acme");
                customer.Ignore(row => row.Tags);
            });
            modelBuilder.Entity<Tgm015Models.Invoice>(invoice =>
            {
                invoice.HasQueryFilter(row => row.TenantId == "acme");
                invoice.Ignore(row => row.BillTo);
                invoice.Ignore(row => row.Lines);
                invoice.HasOne(row => row.Customer).WithMany().HasForeignKey(row => row.CustomerId);
            });
        }).Findings.Should().NotContain(finding => finding.RuleId == "TGM015");

    [Fact]
    public void Suppress_moves_every_finding_to_Suppressed()
    {
        var report = TestModels.ValidateAll<Tgm015Models.TwoForeignKeys>(tg => tg.Suppress("TGM015", Why));

        report.Findings.Should().BeEmpty();
        report.SuppressedWithReasons.Should().HaveCount(2).And.OnlyContain(suppressed => suppressed.Finding.RuleId == "TGM015" && suppressed.Reason == Why);
    }

    [Fact]
    public void Suppress_for_the_dependent_type_leaves_out_its_foreign_keys()
    {
        var report = TestModels.ValidateAll<Tgm015Models.SingleColumn>(tg => tg.Suppress<Tgm015Models.Invoice>("TGM015", Why));

        report.Findings.Should().BeEmpty();
        report.Suppressed.Should().ContainSingle();
    }

    [Fact]
    public void Cross_tenant_context_runs_no_rule()
        => TestModels.ValidateAll<Tgm015Models.SingleColumn>(tg => tg.CrossTenant("a reporting context")).Findings.Should().BeEmpty();

    private static IEnumerable<string> Reported<TContext>(Action<TenantGuardOptionsBuilder>? configure = null)
        where TContext : DbContext
        => TestModels.ValidateAll<TContext>(configure).Findings.Where(finding => finding.RuleId == "TGM015").Select(finding => finding.Message);

    private static IEnumerable<string> Reported(Type context)
        => ((ModelReport)typeof(TestModels).GetMethod(nameof(TestModels.ValidateAll))!.MakeGenericMethod(context).Invoke(null, [null])!)
            .Findings.Where(finding => finding.RuleId == "TGM015").Select(finding => finding.Message);
}
