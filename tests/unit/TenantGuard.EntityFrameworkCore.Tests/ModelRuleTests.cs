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
        => TestModels.Validate<Tgm001Models.Guarded>().Findings.Should().BeEmpty();

    [Fact]
    public void Entity_without_a_filter_is_TGM001()
        => TestModels.Validate<Tgm001Models.Unguarded>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Invoice", "TenantId"));

    [Fact]
    public void Named_tenant_filter_beside_other_named_filters_passes()
        => TestModels.Validate<Tgm001Models.NamedFilters>().Findings.Should().BeEmpty();

    [Fact]
    public void Filter_that_skips_the_tenant_property_is_TGM001()
        => TestModels.Validate<Tgm001Models.SoftDeleteOnly>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Invoice", "TenantId"));

    [Fact]
    public void Keyless_entity_without_a_filter_is_TGM001()
        => TestModels.Validate<Tgm001Models.KeylessUnguarded>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("InvoiceTotal", "TenantId"));

    [Fact]
    public void Keyless_entity_with_a_filter_passes()
        => TestModels.Validate<Tgm001Models.KeylessGuarded>().Findings.Should().BeEmpty();

    [Fact]
    public void Shadow_tenant_property_without_a_filter_is_TGM001()
        => TestModels.Validate<Tgm001Models.ShadowUnguarded>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Note", "TenantId"));

    [Fact]
    public void Shadow_tenant_property_filtered_through_EF_Property_passes()
        => TestModels.Validate<Tgm001Models.ShadowGuarded>().Findings.Should().BeEmpty();

    [Fact]
    public void EF_Property_with_a_captured_name_passes()
        => TestModels.Validate<Tgm001Models.CapturedPropertyName>().Findings.Should().BeEmpty();

    [Fact]
    public void EF_Property_with_a_captured_name_in_a_lifted_comparison_passes()
        => TestModels.Validate<Tgm001Models.CapturedPropertyNameLifted>().Findings.Should().BeEmpty();

    [Fact]
    public void EF_Property_with_a_name_passed_to_a_base_context_helper_passes()
        => TestModels.Validate<Tgm001Models.CapturedPropertyNameFromHelper>().Findings.Should().BeEmpty();

    [Fact]
    public void EF_Property_with_a_captured_name_of_another_property_is_TGM001()
        => TestModels.Validate<Tgm001Models.CapturedOtherPropertyName>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Invoice", "TenantId"));

    [Fact]
    public void Filter_through_a_navigation_counts_as_a_tenant_filter()
        => TestModels.Validate<Tgm001Models.ThroughNavigation>().Findings.Should().BeEmpty();

    [Fact]
    public void Entity_without_a_tenant_property_passes_by_default()
        => TestModels.Validate<Tgm002Models.WithLookup>().Findings.Should().BeEmpty();

    [Fact]
    public void Entity_without_a_tenant_property_is_TGM002_when_all_must_be_scoped()
        => TestModels.Validate<Tgm002Models.WithLookup>(tg => tg.RequireAllEntitiesScoped()).Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM002",
                Severity.Error,
                "Country",
                null,
                "Country has no tenant property and is not declared shared. Add 'TenantId', or declare it with Shared<Country>()."));

    [Fact]
    public void Shared_entity_passes_when_all_must_be_scoped()
        => TestModels.Validate<Tgm002Models.WithLookup>(
            tg => tg.RequireAllEntitiesScoped().Shared<Tgm002Models.Country>()).Findings.Should().BeEmpty();

    [Fact]
    public void Context_member_passes()
        => TestModels.Validate<Tgm003Models.ContextMember>().Findings.Should().BeEmpty();

    [Fact]
    public void Local_captured_in_OnModelCreating_is_TGM003()
        => TestModels.Validate<Tgm003Models.CapturedLocal>().Findings.Should().ContainSingle().Subject.Should().Be(Captured("Invoice", "TenantId"));

    [Fact]
    public void Local_captured_in_a_configuration_class_is_TGM003()
        => TestModels.Validate<Tgm003Models.CapturedInConfiguration>().Findings.Should().ContainSingle().Subject.Should().Be(Captured("Invoice", "TenantId"));

    [Fact]
    public void Captured_null_of_an_interface_the_context_implements_passes()
        => TestModels.Validate<Tgm003Models.ContextInterface>().Findings.Should().BeEmpty();

    [Fact]
    public void Captured_tenant_source_is_TGM003()
        => TestModels.Validate<Tgm003Models.CapturedSource>().Findings.Should().ContainSingle().Subject.Should().Be(Captured("Invoice", "TenantId"));

    [Fact]
    public void Startup_check_reads_an_interface_the_context_implements_as_the_context()
    {
        using var context = TestModels.Create<Tgm003Models.ContextInterface>(options => options.UseTenantGuard(_ => { }));

        context.Model.Should().NotBeNull();
    }

    [Fact]
    public void Startup_check_reports_a_captured_tenant_source()
    {
        using var context = TestModels.Create<Tgm003Models.CapturedSource>(options => options.UseTenantGuard(_ => { }));

        var exception = FluentActions.Invoking(() => context.Model).Should().ThrowExactly<TenantGuardModelException>().Which;

        exception.Report.Errors.Should().ContainSingle().Subject.Should().Be(Captured("Invoice", "TenantId"));
    }

    [Fact]
    public void Static_member_passes()
        => TestModels.Validate<Tgm004Models.StaticMember>().Findings.Should().BeEmpty();

    [Fact]
    public void String_literal_is_TGM004()
        => TestModels.Validate<Tgm004Models.StringLiteral>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM004",
                Severity.Error,
                "Invoice",
                "TenantId",
                "Invoice: the tenant filter compares 'TenantId' to the constant \"acme\". Reference a DbContext member instead."));

    [Fact]
    public void Numeric_literal_is_TGM004()
        => TestModels.Validate<Tgm004Models.NumericLiteral>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM004",
                Severity.Error,
                "Shipment",
                "TenantId",
                "Shipment: the tenant filter compares 'TenantId' to the constant 42. Reference a DbContext member instead."));

    [Fact]
    public void Literal_beside_a_false_constant_under_or_is_TGM004()
        => TestModels.Validate<Tgm004Models.OrElseBuiltAsTenant>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM004",
                Severity.Error,
                "Invoice",
                "TenantId",
                "Invoice: the tenant filter compares 'TenantId' to the constant \"acme\". Reference a DbContext member instead."));

    [Fact]
    public void Filter_made_always_true_by_a_true_constant_under_or_is_TGM001()
        => TestModels.Validate<Tgm004Models.OrElseBuiltWithoutTenant>().Findings.Should().ContainSingle().Subject.Should().Be(
            Unguarded("Invoice", "TenantId"));

    [Fact]
    public void Required_tenant_property_passes()
        => TestModels.Validate<Tgm005Models.RequiredTenant>().Findings.Should().BeEmpty();

    [Fact]
    public void Nullable_tenant_property_is_TGM005()
        => TestModels.Validate<Tgm005Models.NullableTenant>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM005",
                Severity.Warning,
                "Receipt",
                "TenantId",
                "Receipt.TenantId is nullable; rows with no tenant are invisible to every tenant and bypass the write guard."));

    [Fact]
    public void Tenant_property_on_the_root_of_a_hierarchy_passes()
        => TestModels.Validate<Tgm006Models.TenantOnRoot>().Findings.Should().BeEmpty();

    [Fact]
    public void Tenant_property_on_a_derived_type_is_TGM006()
        => TestModels.Validate<Tgm006Models.TenantOnDerived>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM006",
                Severity.Error,
                "Invoice",
                "TenantId",
                "Invoice declares 'TenantId' but filters can only be defined on the root type Document. Move the property to the root."));

    [Fact]
    public void Context_method_passes()
        => TestModels.Validate<Tgm007Models.ContextMethod>().Findings.Should().BeEmpty();

    [Fact]
    public void Comparison_with_another_column_is_TGM007()
        => TestModels.Validate<Tgm007Models.OtherColumn>().Findings.Should().ContainSingle().Subject.Should().Be(NotParameterized("Invoice", "TenantId"));

    [Fact]
    public void Field_of_a_configuration_class_is_TGM007()
        => TestModels.Validate<Tgm007Models.ConfigurationField>().Findings.Should().ContainSingle().Subject.Should().Be(NotParameterized("Invoice", "TenantId"));

    [Fact]
    public void Tenant_property_that_is_never_compared_is_TGM007()
        => TestModels.Validate<Tgm007Models.NullCheckOnly>().Findings.Should().ContainSingle().Subject.Should().Be(NotParameterized("Invoice", "TenantId"));

    [Fact]
    public void Owned_type_without_a_tenant_property_passes()
        => TestModels.Validate<Tgm008Models.OwnedWithoutTenant>().Findings.Should().BeEmpty();

    [Fact]
    public void Owned_type_with_a_tenant_property_is_TGM008()
        => TestModels.Validate<Tgm008Models.OwnedWithTenant>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM008",
                Severity.Warning,
                "BillingAddress",
                "TenantId",
                "BillingAddress is owned by Customer; its 'TenantId' is ignored, isolation follows the owner."));

    [Fact]
    public void Shadow_ownership_key_named_like_the_tenant_property_passes()
        => TestModels.Validate<Tgm008Models.OwnedByTenantRegistry>().Findings.Should().BeEmpty();

    [Fact]
    public void Declared_tenant_property_used_as_the_ownership_key_passes()
        => TestModels.Validate<Tgm008Models.OwnedWithClrOwnershipKey>().Findings.Should().BeEmpty();

    [Fact]
    public void Shadow_ownership_key_named_like_a_configured_tenant_property_passes()
        => TestModels.Validate<Tgm008Models.OwnedByRenamedTenantRegistry>(options => options.TenantPropertyNames("ClubId")).Findings.Should().BeEmpty();

    [Fact]
    public void Owned_tenant_property_beside_the_ownership_key_is_TGM008()
        => TestModels.Validate<Tgm008Models.OwnedByTenantRegistryWithTenant>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
                "TGM008",
                Severity.Warning,
                "BillingAddress",
                "TenantId",
                "BillingAddress is owned by Workspace; its 'TenantId' is ignored, isolation follows the owner."));

    [Fact]
    public void TenantKey_attribute_marks_the_tenant_property()
        => TestModels.Validate<DiscoveryModels.Attributed>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Ledger", "OrgId"));

    [Fact]
    public void TenantKey_attribute_wins_over_the_property_names()
        => TestModels.Validate<DiscoveryModels.AttributeOverName>().Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Journal", "OrgId"));

    [Fact]
    public void Explicit_registration_names_the_tenant_property()
        => TestModels.Validate<DiscoveryModels.Plain>(
                tg => tg.TenantProperty<DiscoveryModels.Account>(account => account.OrgId)).Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Account", "OrgId"));

    [Fact]
    public void Custom_property_names_find_the_tenant_property()
        => TestModels.Validate<DiscoveryModels.Plain>(tg => tg.TenantPropertyNames("OrgId")).Findings.Should().ContainSingle().Subject.Should().Be(Unguarded("Account", "OrgId"));

    [Fact]
    public void Entity_matching_no_property_name_is_not_tenant_scoped()
        => TestModels.Validate<DiscoveryModels.Plain>().Findings.Should().BeEmpty();

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
