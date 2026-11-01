// <copyright file="OptionsBuilderTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using FsCheck;
using FsCheck.Fluent;
using Microsoft.EntityFrameworkCore;
using TenantGuard.EntityFrameworkCore.Tests.Models;
using TenantGuard.EntityFrameworkCore.Tests.Support;

namespace TenantGuard.EntityFrameworkCore.Tests;

// TenantGuardOptionsBuilder and UseTenantGuard.
public sealed class OptionsBuilderTests
{
    [Fact]
    public void TenantPropertyNames_needs_at_least_one_name()
        => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().TenantPropertyNames()).Should().ThrowExactly<ArgumentException>();

    [Fact]
    public void TenantPropertyNames_rejects_any_blank_name()
        => Prop.ForAll(
                Generators.Blank(),
                blank => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().TenantPropertyNames("TenantId", blank)).Should().ThrowExactly<ArgumentException>())
            .QuickCheckThrowOnFailure();

    [Fact]
    public void TenantPropertyNames_keeps_the_names_as_given_and_in_order()
        => Prop.ForAll(
                Generators.NonBlank().Array().Filter(names => names.Length > 0),
                names => new TenantGuardOptionsBuilder().TenantPropertyNames(names).Options.TenantPropertyNames.Should().Equal(names))
            .QuickCheckThrowOnFailure();

    [Fact]
    public void TenantProperty_needs_a_property_of_the_entity()
        => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().TenantProperty<DiscoveryModels.Account>(account => account.OrgId.Trim())).Should().ThrowExactly<ArgumentException>();

    [Fact]
    public void Enforcement_rejects_an_unknown_value()
        => FluentActions.Invoking(() => new TenantGuardOptionsBuilder().Enforcement((Enforcement)42)).Should().ThrowExactly<ArgumentOutOfRangeException>();

    [Fact]
    public void A_second_UseTenantGuard_builds_on_the_first()
    {
        var builder = new DbContextOptionsBuilder();
        builder.UseTenantGuard(tg => tg.TenantPropertyNames("OrgId"));
        builder.UseTenantGuard(tg => tg.Enforcement(Enforcement.Log));

        var options = builder.Options.FindExtension<TenantGuardOptionsExtension>()!.Options;

        options.TenantPropertyNames.Should().ContainSingle().Subject.Should().Be("OrgId");
        options.Enforcement.Should().Be(Enforcement.Log);
    }

    [Fact]
    public void Equal_options_share_an_internal_service_provider()
    {
        var first = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Shared<DiscoveryModels.Account>().Options);
        var second = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Shared<DiscoveryModels.Account>().Options);

        first.Info.ShouldUseSameServiceProvider(second.Info).Should().BeTrue();
        second.Info.GetServiceProviderHashCode().Should().Be(first.Info.GetServiceProviderHashCode());
    }

    [Fact]
    public void Different_options_use_their_own_internal_service_provider()
    {
        var first = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Options);
        var second = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Enforcement(Enforcement.Log).Options);

        first.Info.ShouldUseSameServiceProvider(second.Info).Should().BeFalse();
    }
}
