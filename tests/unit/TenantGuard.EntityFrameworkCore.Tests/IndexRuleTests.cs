// <copyright file="IndexRuleTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// TGM009 and TGM010 against their positive and negative models, with the exact documented message.
public sealed class IndexRuleTests
{
    [Fact]
    public void Entity_keyed_only_by_Id_is_TGM009()
        => TestModels.ValidateAll<Tgm009Models.KeyOnly>().Findings.Should().ContainSingle().Subject.Should().Be(Unindexed("Invoice"));

    [Fact]
    public void Index_with_the_tenant_second_is_TGM009()
        => TestModels.ValidateAll<Tgm009Models.TenantSecond>().Findings.Should().ContainSingle().Subject.Should().Be(Unindexed("Invoice"));

    [Fact]
    public void Index_on_the_tenant_passes()
        => TestModels.ValidateAll<Tgm009Models.TenantIndex>().Findings.Should().BeEmpty();

    [Fact]
    public void Primary_key_that_starts_with_the_tenant_passes()
        => TestModels.ValidateAll<Tgm009Models.TenantFirstKey>().Findings.Should().BeEmpty();

    [Fact]
    public void Foreign_key_index_that_starts_with_the_tenant_passes()
        => TestModels.ValidateAll<Tgm009Models.TenantForeignKey>().Findings.Should().BeEmpty();

    [Fact]
    public void Index_declared_on_a_derived_type_counts()
        => TestModels.ValidateAll<Tgm009Models.IndexOnDerived>().Findings.Should().BeEmpty();

    [Fact]
    public void Entity_mapped_to_a_view_is_skipped()
        => TestModels.ValidateAll<Tgm009Models.MappedToView>().Findings.Should().BeEmpty();

    [Fact]
    public void Keyless_entity_is_skipped()
        => TestModels.ValidateAll<Tgm001Models.KeylessGuarded>().Findings.Should().BeEmpty();

    [Fact]
    public void Model_without_a_relational_provider_is_skipped()
        => TestModels.ValidateWithoutProvider(modelBuilder => modelBuilder.Entity<Tgm009Models.Invoice>(invoice =>
            {
                invoice.HasIndex(row => row.Number).IsUnique();
                invoice.HasQueryFilter(row => row.TenantId == Tenants.Current);
            })).Findings.Should().BeEmpty();

    [Fact]
    public void Unique_index_without_the_tenant_is_TGM010()
        => TestModels.ValidateAll<Tgm010Models.UniqueNumber>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM010",
            Severity.Error,
            "Invoice",
            "TenantId",
            "Invoice: the unique index on (Number) doesn't include 'TenantId', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasIndex(e => new { e.TenantId, e.Number }).IsUnique()."));

    [Fact]
    public void Filtered_unique_index_on_two_columns_is_TGM010()
        => TestModels.ValidateAll<Tgm010Models.UniqueYearAndNumber>().Findings.Should().ContainSingle().Subject.Message.Should().Be(
            "Invoice: the unique index on (Year, Number) doesn't include 'TenantId', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasIndex(e => new { e.TenantId, e.Year, e.Number }).IsUnique().");

    [Fact]
    public void Alternate_key_without_the_tenant_is_TGM010()
        => TestModels.ValidateAll<Tgm010Models.AlternateCode>().Findings.Should().ContainSingle().Subject.Should().Be(new ModelFinding(
            "TGM010",
            Severity.Error,
            "Invoice",
            "TenantId",
            "Invoice: the alternate key (Code) doesn't include 'TenantId', so a value one tenant has used is refused to every other tenant, which tells them it exists. Make it unique per tenant: HasAlternateKey(e => new { e.TenantId, e.Code })."));

    [Fact]
    public void Unique_index_on_a_derived_type_names_that_type()
        => TestModels.ValidateAll<Tgm010Models.UniqueOnDerived>().Findings.Should().ContainSingle().Subject.Message.Should().StartWith("CardPayment: the unique index on (Last4) doesn't include 'TenantId'");

    [Fact]
    public void One_to_one_foreign_key_index_passes()
        => TestModels.ValidateAll<Tgm010Models.OneToOne>().Findings.Should().BeEmpty();

    [Fact]
    public void Unique_index_and_alternate_key_over_a_foreign_key_to_a_tenant_scoped_row_pass()
        => TestModels.ValidateAll<Tgm010Models.UniqueWithinTenantScopedPrincipal>().Findings.Should().BeEmpty();

    [Fact]
    public void Join_table_of_tenant_scoped_rows_passes()
        => TestModels.ValidateAll<Tgm010Models.JoinOfTenantScopedPrincipals>().Findings.Should().BeEmpty();

    [Fact]
    public void Unique_index_over_a_foreign_key_to_a_shared_row_is_TGM010()
        => TestModels.ValidateAll<Tgm010Models.UniqueWithinSharedPrincipal>().Findings.Should().ContainSingle().Subject.Message.Should().StartWith("Office: the unique index on (CountryId, Code) doesn't include 'TenantId'");

    [Fact]
    public void Unique_per_tenant_passes()
        => TestModels.ValidateAll<Tgm010Models.UniquePerTenant>().Findings.Should().BeEmpty();

    [Fact]
    public void Unique_index_with_the_tenant_second_is_only_TGM009()
        => TestModels.ValidateAll<Tgm010Models.UniqueTenantSecond>().Findings.Should().ContainSingle().Subject.Should().Be(Unindexed("Invoice"));

    [Fact]
    public void Startup_check_reports_TGM010_as_an_error()
    {
        using var context = TestModels.Create<Tgm010Models.UniqueNumber>(options => options.UseTenantGuard(_ => { }));

        var act = () => context.Model;

        act.Should().Throw<TenantGuardModelException>().Which.Report.Errors.Should().ContainSingle().Which.RuleId.Should().Be("TGM010");
    }

    private static ModelFinding Unindexed(string entity)
        => new(
            "TGM009",
            Severity.Warning,
            entity,
            "TenantId",
            $"{entity} has no index or key that starts with 'TenantId', so a query that only its tenant filter narrows, such as a list of a tenant's rows, scans every tenant's rows. Add HasIndex(e => e.TenantId), or put 'TenantId' first in an existing index or key; a table read only by its key doesn't need one.");

    private static class Tenants
    {
        public static string Current { get; set; } = "acme";
    }
}
