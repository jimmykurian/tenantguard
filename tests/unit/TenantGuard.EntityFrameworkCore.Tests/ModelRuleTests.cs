// <copyright file="ModelRuleTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// Each rule against its positive and negative models, with the exact documented message.
public sealed class ModelRuleTests
{
    [Fact]
    public void Filter_on_the_tenant_property_passes()
        => Assert.Empty(TestModels.Validate<Tgm001Models.Guarded>().Findings);

    [Fact]
    public void Entity_without_a_filter_is_TGM001()
        => Assert.Equal(
            Unguarded("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm001Models.Unguarded>().Findings));

    [Fact]
    public void Named_tenant_filter_beside_other_named_filters_passes()
        => Assert.Empty(TestModels.Validate<Tgm001Models.NamedFilters>().Findings);

    [Fact]
    public void Filter_that_skips_the_tenant_property_is_TGM001()
        => Assert.Equal(
            Unguarded("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm001Models.SoftDeleteOnly>().Findings));

    [Fact]
    public void Keyless_entity_without_a_filter_is_TGM001()
        => Assert.Equal(
            Unguarded("InvoiceTotal", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm001Models.KeylessUnguarded>().Findings));

    [Fact]
    public void Keyless_entity_with_a_filter_passes()
        => Assert.Empty(TestModels.Validate<Tgm001Models.KeylessGuarded>().Findings);

    [Fact]
    public void Shadow_tenant_property_without_a_filter_is_TGM001()
        => Assert.Equal(
            Unguarded("Note", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm001Models.ShadowUnguarded>().Findings));

    [Fact]
    public void Shadow_tenant_property_filtered_through_EF_Property_passes()
        => Assert.Empty(TestModels.Validate<Tgm001Models.ShadowGuarded>().Findings);

    [Fact]
    public void Filter_through_a_navigation_counts_as_a_tenant_filter()
        => Assert.Empty(TestModels.Validate<Tgm001Models.ThroughNavigation>().Findings);

    [Fact]
    public void Entity_without_a_tenant_property_passes_by_default()
        => Assert.Empty(TestModels.Validate<Tgm002Models.WithLookup>().Findings);

    [Fact]
    public void Entity_without_a_tenant_property_is_TGM002_when_all_must_be_scoped()
        => Assert.Equal(
            new ModelFinding(
                "TGM002",
                Severity.Error,
                "Country",
                null,
                "Country has no tenant property and is not declared shared. Add 'TenantId', or declare it with Shared<Country>()."),
            Assert.Single(TestModels.Validate<Tgm002Models.WithLookup>(tg => tg.RequireAllEntitiesScoped()).Findings));

    [Fact]
    public void Shared_entity_passes_when_all_must_be_scoped()
        => Assert.Empty(TestModels.Validate<Tgm002Models.WithLookup>(
            tg => tg.RequireAllEntitiesScoped().Shared<Tgm002Models.Country>()).Findings);

    [Fact]
    public void Context_member_passes()
        => Assert.Empty(TestModels.Validate<Tgm003Models.ContextMember>().Findings);

    [Fact]
    public void Local_captured_in_OnModelCreating_is_TGM003()
        => Assert.Equal(
            Captured("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm003Models.CapturedLocal>().Findings));

    [Fact]
    public void Local_captured_in_a_configuration_class_is_TGM003()
        => Assert.Equal(
            Captured("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm003Models.CapturedInConfiguration>().Findings));

    [Fact]
    public void Captured_null_of_an_interface_the_context_implements_passes()
        => Assert.Empty(TestModels.Validate<Tgm003Models.ContextInterface>().Findings);

    [Fact]
    public void Captured_tenant_source_is_TGM003()
        => Assert.Equal(
            Captured("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm003Models.CapturedSource>().Findings));

    [Fact]
    public void Startup_check_reads_an_interface_the_context_implements_as_the_context()
    {
        using var context = TestModels.Create<Tgm003Models.ContextInterface>(options => options.UseTenantGuard(_ => { }));

        Assert.NotNull(context.Model);
    }

    [Fact]
    public void Startup_check_reports_a_captured_tenant_source()
    {
        using var context = TestModels.Create<Tgm003Models.CapturedSource>(options => options.UseTenantGuard(_ => { }));

        var exception = Assert.Throws<TenantGuardModelException>(() => context.Model);

        Assert.Equal(Captured("Invoice", "TenantId"), Assert.Single(exception.Report.Findings));
    }

    [Fact]
    public void Static_member_passes()
        => Assert.Empty(TestModels.Validate<Tgm004Models.StaticMember>().Findings);

    [Fact]
    public void String_literal_is_TGM004()
        => Assert.Equal(
            new ModelFinding(
                "TGM004",
                Severity.Error,
                "Invoice",
                "TenantId",
                "Invoice: the tenant filter compares 'TenantId' to the constant \"acme\". Reference a DbContext member instead."),
            Assert.Single(TestModels.Validate<Tgm004Models.StringLiteral>().Findings));

    [Fact]
    public void Numeric_literal_is_TGM004()
        => Assert.Equal(
            new ModelFinding(
                "TGM004",
                Severity.Error,
                "Shipment",
                "TenantId",
                "Shipment: the tenant filter compares 'TenantId' to the constant 42. Reference a DbContext member instead."),
            Assert.Single(TestModels.Validate<Tgm004Models.NumericLiteral>().Findings));

    [Fact]
    public void Required_tenant_property_passes()
        => Assert.Empty(TestModels.Validate<Tgm005Models.RequiredTenant>().Findings);

    [Fact]
    public void Nullable_tenant_property_is_TGM005()
        => Assert.Equal(
            new ModelFinding(
                "TGM005",
                Severity.Warning,
                "Receipt",
                "TenantId",
                "Receipt.TenantId is nullable; rows with no tenant are invisible to every tenant and bypass the write guard."),
            Assert.Single(TestModels.Validate<Tgm005Models.NullableTenant>().Findings));

    [Fact]
    public void Tenant_property_on_the_root_of_a_hierarchy_passes()
        => Assert.Empty(TestModels.Validate<Tgm006Models.TenantOnRoot>().Findings);

    [Fact]
    public void Tenant_property_on_a_derived_type_is_TGM006()
        => Assert.Equal(
            new ModelFinding(
                "TGM006",
                Severity.Error,
                "Invoice",
                "TenantId",
                "Invoice declares 'TenantId' but filters can only be defined on the root type Document. Move the property to the root."),
            Assert.Single(TestModels.Validate<Tgm006Models.TenantOnDerived>().Findings));

    [Fact]
    public void Context_method_passes()
        => Assert.Empty(TestModels.Validate<Tgm007Models.ContextMethod>().Findings);

    [Fact]
    public void Comparison_with_another_column_is_TGM007()
        => Assert.Equal(
            NotParameterized("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm007Models.OtherColumn>().Findings));

    [Fact]
    public void Field_of_a_configuration_class_is_TGM007()
        => Assert.Equal(
            NotParameterized("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm007Models.ConfigurationField>().Findings));

    [Fact]
    public void Tenant_property_that_is_never_compared_is_TGM007()
        => Assert.Equal(
            NotParameterized("Invoice", "TenantId"),
            Assert.Single(TestModels.Validate<Tgm007Models.NullCheckOnly>().Findings));

    [Fact]
    public void Owned_type_without_a_tenant_property_passes()
        => Assert.Empty(TestModels.Validate<Tgm008Models.OwnedWithoutTenant>().Findings);

    [Fact]
    public void Owned_type_with_a_tenant_property_is_TGM008()
        => Assert.Equal(
            new ModelFinding(
                "TGM008",
                Severity.Warning,
                "BillingAddress",
                "TenantId",
                "BillingAddress is owned by Customer; its 'TenantId' is ignored, isolation follows the owner."),
            Assert.Single(TestModels.Validate<Tgm008Models.OwnedWithTenant>().Findings));

    [Fact]
    public void TenantKey_attribute_marks_the_tenant_property()
        => Assert.Equal(
            Unguarded("Ledger", "OrgId"),
            Assert.Single(TestModels.Validate<DiscoveryModels.Attributed>().Findings));

    [Fact]
    public void TenantKey_attribute_wins_over_the_property_names()
        => Assert.Equal(
            Unguarded("Journal", "OrgId"),
            Assert.Single(TestModels.Validate<DiscoveryModels.AttributeOverName>().Findings));

    [Fact]
    public void Explicit_registration_names_the_tenant_property()
        => Assert.Equal(
            Unguarded("Account", "OrgId"),
            Assert.Single(TestModels.Validate<DiscoveryModels.Plain>(
                tg => tg.TenantProperty<DiscoveryModels.Account>(account => account.OrgId)).Findings));

    [Fact]
    public void Custom_property_names_find_the_tenant_property()
        => Assert.Equal(
            Unguarded("Account", "OrgId"),
            Assert.Single(TestModels.Validate<DiscoveryModels.Plain>(tg => tg.TenantPropertyNames("OrgId")).Findings));

    [Fact]
    public void Entity_matching_no_property_name_is_not_tenant_scoped()
        => Assert.Empty(TestModels.Validate<DiscoveryModels.Plain>().Findings);

    private static ModelFinding Unguarded(string entity, string property)
        => new(
            "TGM001",
            Severity.Error,
            entity,
            property,
            $"{entity} has tenant property '{property}' but no query filter references it. Add HasQueryFilter(e => e.{property} == <current tenant>) on the root type.");

    private static ModelFinding Captured(string entity, string property)
        => new(
            "TGM003",
            Severity.Error,
            entity,
            property,
            $"{entity}: the tenant filter captures a local value. The model is cached per context type, so every tenant will see the first tenant's rows. Reference a DbContext member instead.");

    private static ModelFinding NotParameterized(string entity, string property)
        => new(
            "TGM007",
            Severity.Warning,
            entity,
            property,
            $"{entity}: the tenant filter's operand could not be classified; confirm it is evaluated per query.");
}
