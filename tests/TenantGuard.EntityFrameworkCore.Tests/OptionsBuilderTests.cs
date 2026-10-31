// <copyright file="OptionsBuilderTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;
using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests;

// TenantGuardOptionsBuilder and UseTenantGuard.
public sealed class OptionsBuilderTests
{
    [Fact]
    public void TenantPropertyNames_needs_at_least_one_name()
        => Assert.Throws<ArgumentException>(() => new TenantGuardOptionsBuilder().TenantPropertyNames());

    [Fact]
    public void TenantPropertyNames_rejects_a_blank_name()
        => Assert.Throws<ArgumentException>(() => new TenantGuardOptionsBuilder().TenantPropertyNames("TenantId", " "));

    [Fact]
    public void TenantProperty_needs_a_property_of_the_entity()
        => Assert.Throws<ArgumentException>(
            () => new TenantGuardOptionsBuilder().TenantProperty<DiscoveryModels.Account>(account => account.OrgId.Trim()));

    [Fact]
    public void Enforcement_rejects_an_unknown_value()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TenantGuardOptionsBuilder().Enforcement((Enforcement)42));

    [Fact]
    public void A_second_UseTenantGuard_builds_on_the_first()
    {
        var builder = new DbContextOptionsBuilder();
        builder.UseTenantGuard(tg => tg.TenantPropertyNames("OrgId"));
        builder.UseTenantGuard(tg => tg.Enforcement(Enforcement.Log));

        var options = builder.Options.FindExtension<TenantGuardOptionsExtension>()!.Options;

        Assert.Equal("OrgId", Assert.Single(options.TenantPropertyNames));
        Assert.Equal(Enforcement.Log, options.Enforcement);
    }

    [Fact]
    public void Equal_options_share_an_internal_service_provider()
    {
        var first = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Shared<DiscoveryModels.Account>().Options);
        var second = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Shared<DiscoveryModels.Account>().Options);

        Assert.True(first.Info.ShouldUseSameServiceProvider(second.Info));
        Assert.Equal(first.Info.GetServiceProviderHashCode(), second.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void Different_options_use_their_own_internal_service_provider()
    {
        var first = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Options);
        var second = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Enforcement(Enforcement.Log).Options);

        Assert.False(first.Info.ShouldUseSameServiceProvider(second.Info));
    }
}
