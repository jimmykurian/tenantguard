// <copyright file="ValidateCommandTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using NSubstitute;

namespace TenantGuard.Tool.Tests;

// The tool's side: everything it checks before it starts the payload, and how it starts it.
public sealed class ValidateCommandTests : IDisposable
{
    private const string Version = "0.1.0-preview.8";

    private readonly string _app = Directory.CreateTempSubdirectory("tenantguard-app-").FullName;
    private readonly string _tool = Directory.CreateTempSubdirectory("tenantguard-tool-").FullName;
    private readonly IPayloadRunner _runner = Substitute.For<IPayloadRunner>();
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public ValidateCommandTests()
    {
        Directory.CreateDirectory(Path.Combine(_tool, "payload", "net10.0"));
        File.WriteAllBytes(Path.Combine(_tool, "payload", "net10.0", "TenantGuard.Tool.Payload.dll"), []);
    }

    private string Assembly => Path.Combine(_app, "App.dll");

    public void Dispose()
    {
        Directory.Delete(_app, recursive: true);
        Directory.Delete(_tool, recursive: true);
        _output.Dispose();
        _error.Dispose();
    }

    [Fact]
    public void The_payload_runs_under_the_apps_deps_file_and_runtimeconfig_and_its_exit_code_is_the_tools()
    {
        WriteApp(Version);
        IReadOnlyList<string>? arguments = null;
        _runner.Run(Arg.Any<string>(), Arg.Do<IReadOnlyList<string>>(args => arguments = args)).Returns(1);

        var exitCode = Run("validate", "--assembly", Assembly, "--context", "AppDbContext");

        exitCode.Should().Be(1);
        arguments.Should().NotBeNull();
        arguments!.Take(5).Should().Equal("exec", "--depsfile", Path.Combine(_app, "App.deps.json"), "--runtimeconfig", Path.Combine(_app, "App.runtimeconfig.json"));
        string[] payloadArguments =
        [
            Path.Combine(_tool, "payload", "net10.0", "TenantGuard.Tool.Payload.dll"),
            "validate", "--assembly", Assembly, "--startup-assembly", Assembly, "--context", "AppDbContext",
        ];
        arguments.SkipWhile(argument => !argument.EndsWith("TenantGuard.Tool.Payload.dll", StringComparison.Ordinal)).Should().Equal(payloadArguments);
    }

    [Fact]
    public void An_app_without_TenantGuard_is_validated_by_any_version()
    {
        WriteApp(tenantGuard: null);

        Run("validate", "--assembly", Assembly).Should().Be(0);

        _runner.ReceivedWithAnyArgs(1).Run(default!, default!);
    }

    [Fact]
    public void A_different_TenantGuard_version_is_refused_before_the_payload_runs()
    {
        WriteApp("0.1.0-preview.7");

        Run("validate", "--assembly", Assembly).Should().Be(2);

        _error.ToString().Should().Be(
            "The app references TenantGuard.EntityFrameworkCore 0.1.0-preview.7, but this is TenantGuard.Tool 0.1.0-preview.8. "
            + "Install the matching tool: dotnet tool update --global TenantGuard.Tool --version 0.1.0-preview.7"
            + Environment.NewLine);
        _runner.DidNotReceiveWithAnyArgs().Run(default!, default!);
    }

    [Fact]
    public void A_missing_payload_asks_for_a_reinstall()
    {
        WriteApp(Version, "net11.0");

        Run("validate", "--assembly", Assembly).Should().Be(2);

        _error.ToString().Should().StartWith("The tool's net11.0 payload is missing: ").And.EndWith($". Reinstall TenantGuard.Tool.{Environment.NewLine}");
    }

    [Fact]
    public void Help_prints_the_usage_and_passes()
    {
        Run("--help").Should().Be(0);

        _output.ToString().Should().StartWith("Usage: dotnet tenantguard validate --assembly <path>");
        _runner.DidNotReceiveWithAnyArgs().Run(default!, default!);
    }

    [Fact]
    public void A_bad_option_cannot_validate()
    {
        Run("validate").Should().Be(2);

        _error.ToString().Should().Be($"--assembly is required: the path to the assembly that holds the DbContext.{Environment.NewLine}");
    }

    private int Run(params string[] args) => ValidateCommand.Run(args, _output, _error, _runner, Version, _tool);

    private void WriteApp(string? tenantGuard, string targetFramework = "net10.0")
    {
        File.WriteAllBytes(Assembly, []);
        File.WriteAllText(Path.Combine(_app, "App.runtimeconfig.json"), $$$"""{"runtimeOptions":{"tfm":"{{{targetFramework}}}"}}""");
        var library = tenantGuard is null ? string.Empty : $$$""","TenantGuard.EntityFrameworkCore/{{{tenantGuard}}}":{"type":"package"}""";
        File.WriteAllText(Path.Combine(_app, "App.deps.json"), """{"libraries":{"App/1.0.0":{"type":"project"}""" + library + "}}");
    }
}
