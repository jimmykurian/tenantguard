// <copyright file="FinbuckleDiscoveryTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TenantGuard;

namespace Finbuckle.Api.Tests;

// TenantGuard finds the tenant property of a Finbuckle entity by Finbuckle's annotation (SPEC 4.1 step 3),
// matching the annotation by name because the runtime package does not reference Finbuckle.
public sealed class FinbuckleDiscoveryTests
{
    [Fact]
    public void Finbuckle_marks_multi_tenant_entities_with_the_annotation_TenantGuard_matches()
        => Assert.Equal("Finbuckle:MultiTenant", Constants.MultiTenantAnnotationName);

    [Fact]
    public void Finbuckle_names_its_tenant_filter_with_the_name_the_guide_configures()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=:memory:").Options;
        using var context = new AppDbContext(new FixedTenant(tenant: null), options);

        // docs/guides/finbuckle.md sets tenantguard.tenant_filter_names = __tenant__ for TG001.
        Assert.Equal("__tenant__", Finbuckle.MultiTenant.Abstractions.Constants.TenantToken);
        Assert.All(
            context.Model.GetEntityTypes(),
            type => Assert.Contains(type.GetDeclaredQueryFilters(), filter => filter.Key == "__tenant__"));
    }

    [Fact]
    public void Annotation_alone_makes_an_entity_tenant_scoped()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite("DataSource=:memory:").Options;
        using var context = new AppDbContext(new FixedTenant(tenant: null), options);

        // No entity has an OrgId, so only the annotation can name their tenant property; an entity it
        // missed would be TGM002 under RequireAllEntitiesScoped.
        var report = TenantGuardModel.Validate(
            context.Model,
            new TenantGuardOptionsBuilder().TenantPropertyNames("OrgId").RequireAllEntitiesScoped().Options);

        Assert.Empty(report.Findings);
    }
}
