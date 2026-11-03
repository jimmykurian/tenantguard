// <copyright file="CommandLine.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// Parses <c>dotnet tenantguard validate</c>'s options. The reference page,
/// docs/reference/validate-tool.md, lists them.
/// </summary>
internal static class CommandLine
{
    /// <summary>
    /// What <c>--help</c> prints.
    /// </summary>
    public const string Usage = """
        Usage: dotnet tenantguard validate --assembly <path> [options] [-- <app arguments>]

        Runs TenantGuard's model rules on a compiled app's DbContext, without starting the app
        or opening a database connection.

        Options:
          --assembly <path>          The assembly that holds the DbContext (required)
          --startup-assembly <path>  The app's entry assembly, when the context lives in a class library
          --context <name>           The context's name or full name; needed when there is more than one
          --warnings-as-errors       Fail on warnings too
          --help                     Show this help

        Arguments after -- go to the app, as they do with dotnet ef.

        Exit codes: 0 no errors, 1 errors (or warnings under --warnings-as-errors), 2 couldn't validate.
        """;

    private const string MoreHelp = " Run dotnet tenantguard --help for the options.";

    /// <summary>
    /// Parses the arguments the tool was started with.
    /// </summary>
    /// <param name="args">The command line, without the program name.</param>
    /// <returns>The arguments, a request for help, or an error.</returns>
    public static ParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return Fail("Name a command: dotnet tenantguard validate --assembly <path>." + MoreHelp);
        }

        if (IsHelp(args[0]))
        {
            return new ParseResult(null, Help: true, null);
        }

        if (args[0] != "validate")
        {
            return Fail($"Unknown command '{args[0]}'; the tool has one, validate." + MoreHelp);
        }

        string? assembly = null;
        string? startupAssembly = null;
        string? context = null;
        var warningsAsErrors = false;
        var appArguments = new List<string>();

        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--":
                    appArguments.AddRange(args.Skip(i + 1));
                    i = args.Count;
                    break;
                case "--help" or "-h" or "-?":
                    return new ParseResult(null, Help: true, null);
                case "--warnings-as-errors":
                    warningsAsErrors = true;
                    break;
                case "--assembly":
                case "--startup-assembly":
                case "--context":
                    if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        return Fail($"{arg} needs a value." + MoreHelp);
                    }

                    var value = args[++i];
                    var given = arg switch
                    {
                        "--assembly" => assembly,
                        "--startup-assembly" => startupAssembly,
                        _ => context,
                    };
                    if (given is not null)
                    {
                        return Fail($"{arg} is given twice.");
                    }

                    if (arg == "--assembly")
                    {
                        assembly = value;
                    }
                    else if (arg == "--startup-assembly")
                    {
                        startupAssembly = value;
                    }
                    else
                    {
                        context = value;
                    }

                    break;
                default:
                    return Fail($"Unknown option '{arg}'." + MoreHelp);
            }
        }

        if (assembly is null)
        {
            return Fail("--assembly is required: the path to the assembly that holds the DbContext.");
        }

        return new ParseResult(
            new ValidateArguments(assembly, startupAssembly ?? assembly, context, warningsAsErrors, appArguments),
            Help: false,
            null);
    }

    /// <summary>
    /// Writes the arguments back out, the way <see cref="Parse"/> reads them.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The command line, starting with the command.</returns>
    public static IReadOnlyList<string> Format(ValidateArguments arguments)
    {
        var args = new List<string> { "validate", "--assembly", arguments.Assembly, "--startup-assembly", arguments.StartupAssembly };
        if (arguments.Context is not null)
        {
            args.Add("--context");
            args.Add(arguments.Context);
        }

        if (arguments.WarningsAsErrors)
        {
            args.Add("--warnings-as-errors");
        }

        if (arguments.AppArguments.Count > 0)
        {
            args.Add("--");
            args.AddRange(arguments.AppArguments);
        }

        return args;
    }

    private static bool IsHelp(string arg) => arg is "--help" or "-h" or "-?";

    private static ParseResult Fail(string error) => new(null, Help: false, error);
}
