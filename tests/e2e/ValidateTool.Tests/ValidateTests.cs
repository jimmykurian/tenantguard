// <copyright file="ValidateTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ValidateTool.Tests;

// dotnet tenantguard validate on a built app, as docs/reference/validate-tool.md describes it. The
// test project builds for net10.0 and net11.0, and each build checks the fixture of the same target,
// so both of the tool's payloads run.
public sealed class ValidateTests
{
    private const string Tgm001Customer =
        "- TGM001: Customer has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.";

    [Fact]
    public async Task A_context_without_UseTenantGuard_is_checked_under_the_default_options()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "LeakyDbContext");

        run.ExitCode.Should().Be(1);
        run.Output.Should().Be($"{Tgm001Customer}\nTenantGuard validated LeakyDbContext: 1 error, 0 warnings.\n");
        run.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task An_app_that_does_not_reference_TenantGuard_is_checked_under_the_default_options()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.UnreferencedFixture);

        run.ExitCode.Should().Be(1);
        run.Output.Should().Be(
            "- TGM001: Invoice has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.\n"
            + "TenantGuard validated AppDbContext: 1 error, 0 warnings.\n");
        run.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task An_error_the_convention_throws_under_UseTenantGuard_is_reported()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "GuardedDbContext");

        run.ExitCode.Should().Be(1);
        run.Output.Should().Be(
            "- TGM001: Order has tenant property 'TenantId' but no query filter references it. Add HasQueryFilter(e => e.TenantId == <current tenant>) on the root type.\n"
            + "TenantGuard validated GuardedDbContext: 1 error, 0 warnings.\n");
    }

    [Fact]
    public async Task A_warning_passes_and_fails_under_warnings_as_errors()
    {
        const string Expected =
            "- TGM005 (warning): Note.TenantId is nullable: a row with no tenant matches no tenant's filter, but a context with no current tenant can read it. Make 'TenantId' required, or declare host rows with NullTenantIsHost().\n"
            + "TenantGuard validated CleanDbContext: 0 errors, 1 warning.\n";

        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "ValidateTool.Fixture.CleanDbContext");
        var strict = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "CleanDbContext", "--warnings-as-errors");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Be(Expected);
        strict.ExitCode.Should().Be(1);
        strict.Output.Should().Be(Expected);
    }

    [Fact]
    public async Task Declarations_are_printed_and_do_not_fail_the_run()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "DeclaredDbContext", "--warnings-as-errors");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Be(
            "- TGM005 (suppressed: a note without a tenant is a draft): Note.TenantId is nullable: a row with no tenant matches no tenant's filter, but a context with no current tenant can read it. Make 'TenantId' required, or declare host rows with NullTenantIsHost().\n"
            + "Unfiltered on purpose: Membership (maps users to their tenants)\n"
            + "TenantGuard validated DeclaredDbContext: 0 errors, 0 warnings, 1 suppressed.\n");
    }

    [Fact]
    public async Task A_cross_tenant_context_runs_no_rules()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "ReportingDbContext");

        run.ExitCode.Should().Be(0);
        run.Output.Should().Be("TenantGuard validated ReportingDbContext: declared cross-tenant (reports across tenants), so no rules ran.\n");
    }

    [Fact]
    public async Task More_than_one_context_needs_context()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture);

        run.ExitCode.Should().Be(2);
        run.Output.Should().BeEmpty();
        run.Error.Should().Be("The assembly has 5 DbContext types: CleanDbContext, DeclaredDbContext, GuardedDbContext, LeakyDbContext, ReportingDbContext. Pass --context to pick one.\n");
    }

    [Fact]
    public async Task An_unknown_context_lists_the_ones_there_are()
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Fixture, "--context", "AuditDbContext");

        run.ExitCode.Should().Be(2);
        run.Error.Should().Be(
            "ValidateTool.Fixture has no DbContext type named 'AuditDbContext'. Its DbContext types: CleanDbContext, DeclaredDbContext, GuardedDbContext, LeakyDbContext, ReportingDbContext.\n");
    }

    [Fact]
    public async Task A_missing_assembly_cannot_be_validated()
    {
        var missing = Path.Combine(Path.GetTempPath(), "missing", "App.dll");

        var run = await ToolRun.StartAsync("validate", "--assembly", missing);

        run.ExitCode.Should().Be(2);
        run.Error.Should().Be($"Assembly not found: {missing}. Build the app first, and pass the path to its .dll.\n");
    }

    [Theory]
    [InlineData("HandRolled.Api")]
    [InlineData("Finbuckle.Api")]
    public async Task The_samples_pass_with_the_context_from_their_host(string sample)
    {
        var run = await ToolRun.StartAsync("validate", "--assembly", ToolRun.Sample(sample));

        run.ExitCode.Should().Be(0, run.Error);
        run.Output.Should().EndWith("TenantGuard validated AppDbContext: 0 errors, 0 warnings.\n");
    }
}
