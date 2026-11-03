// <copyright file="AppLayoutTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool.Tests;

// What the tool reads from the app's build output before it starts the payload.
public sealed class AppLayoutTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("tenantguard-tool-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Theory]
    [InlineData("""{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App"}}}""", "net10.0")]
    [InlineData("""{"runtimeOptions":{}}""", null)]
    [InlineData("""{}""", null)]
    public void The_target_framework_comes_from_the_runtimeconfig(string json, string? expected)
        => AppLayout.ReadTargetFramework(json).Should().Be(expected);

    [Theory]
    [InlineData("TenantGuard.EntityFrameworkCore/0.1.0-preview.8", "package", "0.1.0-preview.8")]
    [InlineData("TenantGuard.EntityFrameworkCore/0.1.0-preview.8", "project", "0.1.0-preview.8")]
    [InlineData("Microsoft.EntityFrameworkCore/10.0.14", "package", null)]
    public void The_TenantGuard_version_comes_from_the_deps_file(string library, string type, string? expected)
        => AppLayout.ReadPackageVersion(
                """{"libraries":{"App/1.0.0":{"type":"project"},""" + $"\"{library}\":{{\"type\":\"{type}\"}}" + "}}",
                "TenantGuard.EntityFrameworkCore")
            .Should().Be(expected);

    [Fact]
    public void The_layout_is_read_from_beside_the_startup_assembly()
    {
        var app = Write("net11.0", "TenantGuard.EntityFrameworkCore/0.1.0-preview.8");

        AppLayout.TryRead(app, out var layout, out var error).Should().BeTrue(error);

        layout.Should().Be(new AppLayout(
            Path.Combine(_directory, "App.deps.json"),
            Path.Combine(_directory, "App.runtimeconfig.json"),
            "net11.0",
            "0.1.0-preview.8"));
    }

    [Fact]
    public void A_missing_deps_file_names_the_file()
    {
        var app = Write("net10.0", null);
        File.Delete(Path.Combine(_directory, "App.deps.json"));

        AppLayout.TryRead(app, out _, out var error).Should().BeFalse();

        error.Should().Be(
            "App.deps.json isn't next to App.dll. Pass the app's build output, as dotnet build writes it, or name the app's entry assembly with --startup-assembly.");
    }

    [Fact]
    public void An_unsupported_target_framework_is_refused()
    {
        var app = Write("net8.0", null);

        AppLayout.TryRead(app, out _, out var error).Should().BeFalse();

        error.Should().Be("App.dll targets net8.0; the tool supports net10.0 and net11.0.");
    }

    private string Write(string targetFramework, string? tenantGuard)
    {
        var app = Path.Combine(_directory, "App.dll");
        File.WriteAllBytes(app, []);
        File.WriteAllText(Path.Combine(_directory, "App.runtimeconfig.json"), $$$"""{"runtimeOptions":{"tfm":"{{{targetFramework}}}"}}""");
        var library = tenantGuard is null ? string.Empty : $$$""","{{{tenantGuard}}}":{"type":"package"}""";
        File.WriteAllText(Path.Combine(_directory, "App.deps.json"), """{"libraries":{"App/1.0.0":{"type":"project"}""" + library + "}}");
        return app;
    }
}
