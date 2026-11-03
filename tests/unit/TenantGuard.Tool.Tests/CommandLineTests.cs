// <copyright file="CommandLineTests.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using FsCheck;
using FsCheck.Fluent;

namespace TenantGuard.Tool.Tests;

// The options in docs/reference/validate-tool.md.
public sealed class CommandLineTests
{
    [Fact]
    public void The_startup_assembly_defaults_to_the_assembly()
    {
        var parsed = CommandLine.Parse(["validate", "--assembly", "App.dll"]);

        parsed.Arguments.Should().BeEquivalentTo(new ValidateArguments("App.dll", "App.dll", null, false, []));
    }

    [Fact]
    public void Every_option_and_the_app_arguments_are_read()
    {
        var parsed = CommandLine.Parse(
            ["validate", "--assembly", "Data.dll", "--startup-assembly", "App.dll", "--context", "AppDbContext", "--warnings-as-errors", "--", "--environment", "Ci"]);

        parsed.Arguments.Should().BeEquivalentTo(
            new ValidateArguments("Data.dll", "App.dll", "AppDbContext", true, ["--environment", "Ci"]));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void Help_is_asked_for_before_or_after_the_command(string help)
    {
        CommandLine.Parse([help]).Help.Should().BeTrue();
        CommandLine.Parse(["validate", "--assembly", "App.dll", help]).Help.Should().BeTrue();
    }

    [Theory]
    [InlineData(new string[0], "Name a command: dotnet tenantguard validate --assembly <path>. Run dotnet tenantguard --help for the options.")]
    [InlineData(new[] { "check" }, "Unknown command 'check'; the tool has one, validate. Run dotnet tenantguard --help for the options.")]
    [InlineData(new[] { "validate" }, "--assembly is required: the path to the assembly that holds the DbContext.")]
    [InlineData(new[] { "validate", "--assembly" }, "--assembly needs a value. Run dotnet tenantguard --help for the options.")]
    [InlineData(new[] { "validate", "--context", "--assembly", "App.dll" }, "--context needs a value. Run dotnet tenantguard --help for the options.")]
    [InlineData(new[] { "validate", "--assembly", "App.dll", "--assembly", "Other.dll" }, "--assembly is given twice.")]
    [InlineData(new[] { "validate", "--assembly", "App.dll", "--verbose" }, "Unknown option '--verbose'. Run dotnet tenantguard --help for the options.")]
    public void A_bad_command_line_says_what_is_wrong(string[] args, string error)
    {
        var parsed = CommandLine.Parse(args);

        parsed.Arguments.Should().BeNull();
        parsed.Help.Should().BeFalse();
        parsed.Error.Should().Be(error);
    }

    [Fact]
    public void Format_writes_what_Parse_reads()
        => Prop.ForAll(
                Arguments(),
                arguments => CommandLine.Parse(CommandLine.Format(arguments)).Arguments.Should().BeEquivalentTo(arguments))
            .QuickCheckThrowOnFailure();

    // Paths, names and app arguments that aren't options themselves, as the tool's own call passes them.
    private static Arbitrary<ValidateArguments> Arguments()
    {
        var value = Gen.Elements("App.dll", "/build/out/Data.dll", "C:\\out\\App.dll", "AppDbContext", "Acme.Data.AppDbContext", "my app.dll");
        var appArgument = Gen.Elements("--environment", "Ci", "--", "-x", "value with spaces");
        return (from assembly in value
                from startup in value
                from context in Gen.OneOf(Gen.Constant<string?>(null), value.Select(v => (string?)v))
                from warningsAsErrors in Gen.Elements(true, false)
                from appArguments in Gen.ListOf(appArgument)
                select new ValidateArguments(assembly, startup, context, warningsAsErrors, [.. appArguments]))
            .ToArbitrary();
    }
}
