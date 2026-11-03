// <copyright file="PerTenantModelTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static TenantGuard.EntityFrameworkCore.Tests.Models.PerTenantModels;

namespace TenantGuard.EntityFrameworkCore.Tests;

// AllowCapturedValues: TGM003 is left out only for a context whose model is its tenant's own, and the
// write guard blocks a save through a model built for another tenant.
public sealed class PerTenantModelTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public PerTenantModelTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Tenant_keyed_model_cache_allows_the_captured_tenant()
    {
        using var context = Create(_connection, "acme", typeof(TenantKeyFactory), Allowed);

        context.Model.Should().NotBeNull();
    }

    [Fact]
    public void Each_tenant_gets_a_model_of_its_own_that_remembers_its_tenant()
    {
        using var acme = Create(_connection, "acme", typeof(TenantKeyFactory), Allowed);
        using var globex = Create(_connection, "globex", typeof(TenantKeyFactory), Allowed);

        acme.Model.Should().NotBeSameAs(globex.Model);
        Entities(acme).ModelTenant.Should().Be("acme");
        Entities(globex).ModelTenant.Should().Be("globex");
    }

    [Theory]
    [InlineData("without the option")]
    [InlineData("with EF Core's own key")]
    [InlineData("with a tuple key that leaves the tenant out")]
    [InlineData("with no current tenant")]
    [InlineData("without CurrentTenant")]
    public void Otherwise_the_captured_tenant_is_TGM003(string setup)
    {
        // CurrentTenant doesn't key EF Core's service provider, so the last case uses a tenant no other
        // test does, and builds a model of its own.
        using var context = setup switch
        {
            "without the option" => Create(_connection, "acme", typeof(TenantKeyFactory), tg => tg.CurrentTenant(Tenant)),
            "with EF Core's own key" => Create(_connection, "acme", keyFactory: null, Allowed),
            "with a tuple key that leaves the tenant out" => Create(_connection, "acme", typeof(VersionKeyFactory), Allowed),
            "with no current tenant" => Create(_connection, tenant: null, typeof(TenantKeyFactory), Allowed),
            _ => Create(_connection, "initech", typeof(TenantKeyFactory), tg => tg.AllowCapturedValues()),
        };

        FluentActions.Invoking(() => context.Model).Should().Throw<TenantGuardModelException>()
            .Which.Report.Errors.Should().ContainSingle().Which.RuleId.Should().Be("TGM003");
    }

    [Fact]
    public void Validate_has_no_context_to_check_so_it_reports_TGM003()
    {
        using var context = Create(_connection, "acme", typeof(TenantKeyFactory), Allowed);

        TenantGuardModel.Validate(context.Model, new TenantGuardOptionsBuilder().AllowCapturedValues().Options)
            .Errors.Should().ContainSingle().Which.RuleId.Should().Be("TGM003");
    }

    [Fact]
    public async Task Each_tenant_saves_through_its_own_model()
    {
        await using var acme = Create(_connection, "acme", typeof(TenantKeyFactory), Allowed);
        await acme.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await using var globex = Create(_connection, "globex", typeof(TenantKeyFactory), Allowed);

        acme.Entries.Add(new Entry { TenantId = "acme" });
        globex.Entries.Add(new Entry { TenantId = "globex" });
        await acme.SaveChangesAsync(TestContext.Current.CancellationToken);
        await globex.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await globex.Entries.Select(entry => entry.TenantId).ToListAsync(TestContext.Current.CancellationToken)).Should().Equal("globex");
    }

    [Fact]
    public void A_save_through_another_tenants_model_is_blocked()
    {
        using var acme = Create(_connection, "acme", typeof(SharedKeyFactory), Allowed);
        _ = acme.Model;
        using var globex = Create(_connection, "globex", typeof(SharedKeyFactory), Allowed);

        globex.Model.Should().BeSameAs(acme.Model);
        globex.Entries.Add(new Entry { TenantId = "globex" });
        var exception = FluentActions.Invoking(() => globex.SaveChanges()).Should().ThrowExactly<TenantIsolationViolationException>().Which;

        exception.Violations.Should().BeEmpty();
        exception.Message.Should().Be(
            "TenantGuard blocked a save to Ledger: its model was built for tenant \"acme\", so its query filters read \"acme\"'s rows, "
            + "but the current tenant is \"globex\". Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.");
    }

    [Fact]
    public async Task A_bypass_lets_a_save_through_another_tenants_model()
    {
        await using var acme = Create(_connection, "acme", typeof(SharedKeyFactory), Allowed);
        await acme.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await using var globex = Create(_connection, "globex", typeof(SharedKeyFactory), Allowed);

        globex.Entries.Add(new Entry { TenantId = "globex" });
        using (globex.TenantGuard().Bypass("migrating rows between tenants"))
        {
            await globex.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public void The_option_takes_part_in_the_service_provider_key()
    {
        var first = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().Options);
        var second = new TenantGuardOptionsExtension(new TenantGuardOptionsBuilder().AllowCapturedValues().Options);

        first.Info.ShouldUseSameServiceProvider(second.Info).Should().BeFalse();
    }

    private static void Allowed(TenantGuardOptionsBuilder tg) => tg.AllowCapturedValues().CurrentTenant(Tenant);

    private static object? Tenant(DbContext context) => ((Ledger)context).TenantId;

    private static TenantGuardEntities Entities(DbContext context)
        => (TenantGuardEntities)context.Model.FindRuntimeAnnotationValue(TenantGuardEntities.AnnotationName)!;
}
