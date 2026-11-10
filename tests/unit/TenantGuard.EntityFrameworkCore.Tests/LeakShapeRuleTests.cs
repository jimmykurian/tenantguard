// <copyright file="LeakShapeRuleTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// TGM010 on a natural primary key, and TGM011 to TGM014, against their positive and negative models,
// with the exact documented messages.
public sealed class LeakShapeRuleTests
{
    private const string SwitchMessage = "Invoice: the tenant filter can be switched off: it is ORed with a condition that doesn't read the row, and while that condition is true, every tenant's rows are returned. Confirm it is true only on purpose.";

    private const string OpenMessage = "Invoice: the tenant filter returns every tenant's rows when there is no current tenant: it is ORed with a null check of the value 'TenantId' is compared with. Compare 'TenantId' with the current tenant alone, so a context with no tenant sees no tenant's rows.";

    [Fact]
    public void Natural_primary_key_without_the_tenant_is_TGM010()
        => TestModels.ValidateAll<Tgm010KeyModels.NaturalKey>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM010",
            Severity.Error,
            "ExternalLogin",
            "TenantId",
            "ExternalLogin: the primary key (LoginProvider, ProviderKey) doesn't include 'TenantId' and its values aren't generated, so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasKey(e => new { e.TenantId, e.LoginProvider, e.ProviderKey })."));

    [Fact]
    public void Natural_primary_key_per_tenant_passes()
        => TestModels.ValidateAll<Tgm010KeyModels.NaturalKeyPerTenant>().Findings.Should().BeEmpty();

    [Fact]
    public void Guid_primary_key_the_app_assigns_passes()
        => TestModels.ValidateAll<Tgm010KeyModels.AssignedGuid>().Findings.Should().BeEmpty();

    [Fact]
    public void Primary_key_shared_with_a_tenant_scoped_principal_passes()
        => TestModels.ValidateAll<Tgm010KeyModels.SharedPrimaryKey>().Findings.Should().BeEmpty();

    [Fact]
    public void Generated_primary_key_passes()
        => TestModels.ValidateAll<Tgm009Models.TenantIndex>().Findings.Should().BeEmpty();

    [Fact]
    public void View_without_a_tenant_property_is_TGM011()
        => TestModels.Validate<Tgm011Models.TenantFreeView>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM011",
            Severity.Warning,
            "InvoiceTotal",
            null,
            "InvoiceTotal is mapped to the view 'invoice_totals' and has no tenant property, so it returns every tenant's rows. Select 'TenantId' in it and filter on it, or declare it with Shared<InvoiceTotal>()."));

    [Fact]
    public void SQL_query_without_a_tenant_property_is_TGM011()
        => TestModels.Validate<Tgm011Models.TenantFreeQuery>().Findings.Should().ContainSingle().Subject.Message.Should().StartWith("InvoiceTotal is mapped to a SQL query and has no tenant property");

    [Fact]
    public void View_with_a_tenant_filter_passes()
        => TestModels.Validate<Tgm011Models.TenantView>().Findings.Should().BeEmpty();

    [Fact]
    public void Shared_view_passes()
        => TestModels.Validate<Tgm011Models.TenantFreeView>(tg => tg.Shared<Tgm011Models.InvoiceTotal>()).Findings.Should().BeEmpty();

    [Fact]
    public void View_in_a_model_without_tenant_data_passes()
        => TestModels.Validate<Tgm011Models.NoTenantData>().Findings.Should().BeEmpty();

    [Fact]
    public void View_is_only_TGM002_when_all_must_be_scoped()
        => TestModels.Validate<Tgm011Models.TenantFreeView>(tg => tg.RequireAllEntitiesScoped()).Findings.Select(finding => finding.RuleId).Should().Equal("TGM002");

    [Fact]
    public void View_without_a_relational_provider_is_skipped()
        => TestModels.ValidateWithoutProvider(modelBuilder =>
            {
                modelBuilder.Entity<Tgm011Models.Invoice>().HasQueryFilter(row => row.TenantId == "acme");
                modelBuilder.Entity<Tgm011Models.InvoiceTotal>().HasNoKey();
            }).Findings.Where(finding => finding.RuleId == "TGM011").Should().BeEmpty();

    [Theory]
    [InlineData(typeof(Tgm012Models.Switch))]
    [InlineData(typeof(Tgm012Models.NegatedAnd))]
    public void Filter_ORed_with_a_switch_is_TGM012(Type context)
        => Validate(context).Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding("TGM012", Severity.Warning, "Invoice", "TenantId", SwitchMessage));

    [Theory]
    [InlineData(typeof(Tgm012Models.RowCondition))]
    [InlineData(typeof(Tgm012Models.NegatedOr))]
    [InlineData(typeof(Tgm012Models.GuardedSwitch))]
    [InlineData(typeof(Tgm012Models.GuardedNullCheck))]
    [InlineData(typeof(Tgm012Models.NegatedNullCheck))]
    public void Filter_that_cannot_open_up_passes(Type context)
        => Validate(context).Findings.Should().BeEmpty();

    [Theory]
    [InlineData(typeof(Tgm012Models.OpenWithoutTenant))]
    [InlineData(typeof(Tgm012Models.OpenWhenEmpty))]
    public void Filter_ORed_with_a_null_check_of_the_tenant_is_TGM013(Type context)
        => Validate(context).Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding("TGM013", Severity.Warning, "Invoice", "TenantId", OpenMessage));

    [Fact]
    public void HasValue_check_of_a_nullable_tenant_is_TGM013()
        => TestModels.Validate<Tgm012Models.OpenWithoutGuid>().Findings.Select(finding => finding.RuleId).Should().Equal("TGM013");

    [Theory]
    [InlineData(typeof(Tgm012Models.SwitchAndOpen))]
    [InlineData(typeof(Tgm012Models.SwitchAndOpenRegrouped))]
    public void Switch_beside_a_null_check_is_TGM012_and_TGM013(Type context)
        => Validate(context).Findings.Select(finding => finding.RuleId).Should().Equal("TGM012", "TGM013");

    [Fact]
    public void Suppressing_TGM013_leaves_TGM012()
        => TestModels.Validate<Tgm012Models.SwitchAndOpen>(tg => tg.Suppress("TGM013", "an admin context sees every tenant")).Findings.Select(finding => finding.RuleId).Should().Equal("TGM012");

    [Fact]
    public void Unfiltered_type_skips_TGM012()
        => TestModels.Validate<Tgm012Models.Switch>(tg => tg.Unfiltered<Tgm012Models.Invoice>("dispatched for every tenant")).Findings.Should().BeEmpty();

    [Fact]
    public void Dependent_of_a_tenant_scoped_principal_without_a_filter_is_TGM014()
        => TestModels.Validate<Tgm014Models.Unfiltered>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM014",
            Severity.Warning,
            "OrderLine",
            null,
            "OrderLine has no tenant property and no query filter that reaches one, but its rows belong to tenant-scoped Order through the required foreign key (OrderId), so Set<OrderLine>() returns every tenant's rows. Filter it through Order, give it 'TenantId' and a filter, or declare it with Shared<OrderLine>()."));

    [Fact]
    public void Dependent_filtered_through_its_principal_passes()
        => TestModels.Validate<Tgm014Models.ThroughPrincipal>().Findings.Should().BeEmpty();

    [Fact]
    public void Dependent_on_an_optional_foreign_key_passes()
        => TestModels.Validate<Tgm014Models.Optional>().Findings.Should().BeEmpty();

    [Fact]
    public void Shared_dependent_passes()
        => TestModels.Validate<Tgm014Models.Unfiltered>(tg => tg.Shared<Tgm014Models.OrderLine>()).Findings.Should().BeEmpty();

    [Fact]
    public void Join_entity_type_of_a_many_to_many_relationship_passes()
        => TestModels.Validate<Tgm014Models.ManyToMany>().Findings.Should().BeEmpty();

    [Fact]
    public void Dependent_is_only_TGM002_when_all_must_be_scoped()
        => TestModels.Validate<Tgm014Models.Unfiltered>(tg => tg.RequireAllEntitiesScoped()).Findings.Select(finding => finding.RuleId).Should().Equal("TGM002");

    private static ModelReport Validate(Type context)
        => (ModelReport)typeof(TestModels).GetMethod(nameof(TestModels.Validate))!.MakeGenericMethod(context).Invoke(null, [null])!;
}
