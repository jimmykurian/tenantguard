// <copyright file="TenantValuesTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using FsCheck;
using FsCheck.Fluent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using static TenantGuard.Testing.Tests.Models.ProbeModels;

namespace TenantGuard.Testing.Tests;

// How the probe turns a WithTenants string into a value of the tenant property's type, over
// generated tenants.
public sealed class TenantValuesTests
{
    [Fact]
    public void A_string_tenant_property_takes_the_tenant_as_given()
    {
        var property = TenantProperty<Customer>();

        Prop.ForAll(
                ArbMap.Default.ArbFor<NonNull<string>>(),
                tenant => TenantValues.Convert(tenant.Get, property).Should().Be(tenant.Get))
            .QuickCheckThrowOnFailure();
    }

    [Fact]
    public void A_Guid_tenant_property_takes_the_Guid_the_tenant_names()
    {
        var property = TenantProperty<Ledger>();

        Prop.ForAll(
                ArbMap.Default.ArbFor<Guid>(),
                id => TenantValues.Convert(id.ToString(), property).Should().Be(id))
            .QuickCheckThrowOnFailure();
    }

    [Fact]
    public void A_tenant_that_is_not_a_Guid_names_the_tenant_and_the_property()
    {
        var property = TenantProperty<Ledger>();

        Prop.ForAll(
                ArbMap.Default.ArbFor<NonNull<string>>().Filter(tenant => !Guid.TryParse(tenant.Get, out _)),
                tenant => FluentActions.Invoking(() => TenantValues.Convert(tenant.Get, property))
                    .Should().ThrowExactly<InvalidOperationException>()
                    .Which.Message.Should().Be($"The tenant \"{tenant.Get}\" is not a Guid, the type of Ledger.TenantId. Pass WithTenants values the tenant property can hold."))
            .QuickCheckThrowOnFailure();
    }

    private static IReadOnlyProperty TenantProperty<TEntity>()
        where TEntity : class
    {
        // A ModelBuilder without conventions discovers nothing, so the property is mapped by name.
        var builder = new ModelBuilder();
        return builder.Entity<TEntity>().Property("TenantId").Metadata;
    }
}
