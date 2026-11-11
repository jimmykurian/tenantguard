// <copyright file="ValidationErrorTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

extern alias payload;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Validation = payload::TenantGuard.Tool.Validation;

namespace TenantGuard.Tool.Tests;

// The payload's messages when it can't create the context (docs/reference/validate-tool.md, "Exit
// codes"). The factories are given, not scanned: two for one context make EF Core fail for every
// context of the assembly they're in.
public sealed class ValidationErrorTests : IDisposable
{
    private const string Design = "Microsoft.EntityFrameworkCore.Design, Version=10.0.14.0, Culture=neutral, PublicKeyToken=adb9793829ddae60";

    private readonly string _directory = Directory.CreateTempSubdirectory("tenantguard-deps-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Design_missing_from_the_deps_file_says_to_reference_it()
        => Validation.LoadError(new FileNotFoundException("Could not load file or assembly.", Design), "App", "AppDbContext", listsDesign: false)
            .Should().Be("App doesn't reference Microsoft.EntityFrameworkCore.Design. Add it to the startup project, as dotnet ef needs, so the tool can create the context the way the app does.");

    [Fact]
    public void Design_the_app_references_at_another_version_is_reported_as_the_load_failure()
        => Validation.LoadError(new FileNotFoundException("Could not load file or assembly 'Microsoft.EntityFrameworkCore.Design, Version=10.0.14.0'.", Design), "App", "AppDbContext", listsDesign: true)
            .Should().Be("Couldn't load Microsoft.EntityFrameworkCore.Design, which creating AppDbContext needs: Could not load file or assembly 'Microsoft.EntityFrameworkCore.Design, Version=10.0.14.0'.");

    [Fact]
    public void Any_other_assembly_that_fails_to_load_is_named()
        => Validation.LoadError(new FileLoadException("The located assembly's manifest definition does not match.", "Acme.Data, Version=2.0.0.0"), "App", "AppDbContext", listsDesign: true)
            .Should().Be("Couldn't load Acme.Data, which creating AppDbContext needs: The located assembly's manifest definition does not match.");

    [Fact]
    public void A_load_failure_wrapped_by_a_factory_is_found()
    {
        var notFound = new FileNotFoundException("Could not load file or assembly.", Design);

        Validation.LoadFailure(new System.Reflection.TargetInvocationException(new InvalidOperationException("The factory failed.", notFound))).Should().BeSameAs(notFound);
        Validation.LoadFailure(new InvalidOperationException("No connection string.")).Should().BeNull();
    }

    [Theory]
    [InlineData("/app/settings.json")]
    [InlineData("settings.json")]
    public void A_missing_file_that_is_not_an_assembly_is_not_a_load_failure(string file)
    {
        Validation.LoadFailure(new System.Reflection.TargetInvocationException(new FileNotFoundException("Could not find the file.", file))).Should().BeNull();
    }

    [Fact]
    public void The_startup_assemblys_factory_comes_before_the_contexts_assemblys()
        => Validation.CreateError(new InvalidOperationException("No connection string."), typeof(AppDbContext), "App", [.. Factories((typeof(AppDbContext), typeof(FirstFactory))), .. Factories((typeof(AppDbContext), typeof(SecondFactory)))])
            .Should().Be("Couldn't create AppDbContext through FirstFactory: No connection string.");

    [Fact]
    public void Two_factories_for_a_context_are_named()
        => Validation.CreateError(new ArgumentException("An item with the same key has already been added."), typeof(AuditDbContext), "App", Factories((typeof(AppDbContext), typeof(FirstFactory)), (typeof(AppDbContext), typeof(SecondFactory))))
            .Should().Be("App has 2 IDesignTimeDbContextFactory<AppDbContext> types: FirstFactory, SecondFactory. Keep one, so the tool can create the context the way dotnet ef does.");

    [Fact]
    public void A_failure_in_the_contexts_factory_names_the_factory_without_the_hint()
        => Validation.CreateError(new InvalidOperationException("No connection string."), typeof(AppDbContext), "App", Factories((typeof(AppDbContext), typeof(FirstFactory))))
            .Should().Be("Couldn't create AppDbContext through FirstFactory: No connection string.");

    [Fact]
    public void A_failure_with_no_factory_gives_the_hint()
        => Validation.CreateError(new InvalidOperationException("No connection string."), typeof(AuditDbContext), "App", Factories((typeof(AppDbContext), typeof(FirstFactory))))
            .Should().Be("Couldn't create AuditDbContext: No connection string. Add an IDesignTimeDbContextFactory<AuditDbContext> to the startup assembly, or make the app's host build without configuration CI doesn't have.");

    [Fact]
    public void Factories_are_found_by_the_context_they_create()
        => Validation.Factories(typeof(ValidationErrorTests).Assembly).Should().ContainSingle(group => group.Key == typeof(AppDbContext))
            .Which.Should().BeEquivalentTo([typeof(FirstFactory), typeof(SecondFactory)]);

    [Theory]
    [InlineData("""{ "libraries": { "Microsoft.EntityFrameworkCore.Design/10.0.13": {} } }""", true)]
    [InlineData("""{ "libraries": { "Microsoft.EntityFrameworkCore/10.0.13": {} } }""", false)]
    public void The_deps_file_is_read_for_Design(string deps, bool listed)
    {
        var path = Path.Combine(_directory, "App.deps.json");
        File.WriteAllText(path, deps);

        Validation.ListsDesign(path).Should().Be(listed);
    }

    [Fact]
    public void No_deps_file_lists_no_Design()
        => Validation.ListsDesign(Path.Combine(_directory, "Missing.deps.json")).Should().BeFalse();

    private static List<IGrouping<Type, Type>> Factories(params (Type Context, Type Factory)[] factories)
        => [.. factories.GroupBy(pair => pair.Context, pair => pair.Factory)];

    internal sealed class AppDbContext : DbContext;

    internal sealed class AuditDbContext : DbContext;

    internal sealed class FirstFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args) => new();
    }

    internal sealed class SecondFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args) => new();
    }
}
