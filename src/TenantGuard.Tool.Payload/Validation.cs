// <copyright file="Validation.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Tool;

/// <summary>
/// Finds the context, creates it the way <c>dotnet ef</c> does, runs the model rules on it and
/// prints the report.
/// </summary>
internal static class Validation
{
    private const string DesignAssembly = "Microsoft.EntityFrameworkCore.Design";

    /// <summary>
    /// Runs the validation.
    /// </summary>
    /// <param name="args">The tool's arguments.</param>
    /// <param name="output">Where the findings and the summary go.</param>
    /// <param name="error">Where a reason it couldn't validate goes.</param>
    /// <param name="load">Loads an assembly from its path.</param>
    /// <returns>The exit code.</returns>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error, Func<string, Assembly> load)
    {
        var parsed = CommandLine.Parse(args);
        if (parsed.Arguments is not { } arguments)
        {
            error.WriteLine(parsed.Error ?? CommandLine.Usage);
            return ExitCodes.CouldNotValidate;
        }

        Assembly assembly;
        Assembly startupAssembly;
        try
        {
            assembly = load(arguments.Assembly);
            startupAssembly = arguments.StartupAssembly == arguments.Assembly ? assembly : load(arguments.StartupAssembly);
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException)
        {
            error.WriteLine($"Couldn't load the app's assembly: {exception.Message}");
            return ExitCodes.CouldNotValidate;
        }

        if (!ContextTypes.TrySelect(assembly, arguments.Context, out var contextType, out var selectError))
        {
            error.WriteLine(selectError);
            return ExitCodes.CouldNotValidate;
        }

        DbContext context;
        try
        {
            context = CreateContext(contextType!, startupAssembly, arguments.AppArguments, error);
        }
        catch (Exception exception) when (ModelCheck.FindModelException(exception) is { } modelException)
        {
            // The activator builds the model while it creates the context, so under Enforcement.Throw
            // the convention's exception, and its report, arrive here.
            return Report(output, contextType!.Name, modelException.Report, arguments.WarningsAsErrors);
        }
        catch (FileNotFoundException exception) when (exception.FileName?.StartsWith(DesignAssembly, StringComparison.Ordinal) == true)
        {
            error.WriteLine(
                $"{startupAssembly.GetName().Name} doesn't reference {DesignAssembly}. "
                + "Add it to the startup project, as dotnet ef needs, so the tool can create the context the way the app does.");
            return ExitCodes.CouldNotValidate;
        }
        catch (Exception exception)
        {
            var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            error.WriteLine(
                $"Couldn't create {contextType!.Name}: {cause.Message} "
                + $"Add an IDesignTimeDbContextFactory<{contextType.Name}> to the startup assembly, "
                + "or make the app's host build without configuration CI doesn't have.");
            return ExitCodes.CouldNotValidate;
        }

        using (context)
        {
            var report = ModelCheck.Run(context, contextType!);
            if (report is null)
            {
                error.WriteLine(
                    $"{contextType!.Name} uses a compiled model (UseModel), and the model rules don't run on a compiled model. "
                    + "Validate a configuration of the context without UseModel.");
                return ExitCodes.CouldNotValidate;
            }

            return Report(output, contextType!.Name, report, arguments.WarningsAsErrors);
        }
    }

    private static int Report(TextWriter output, string contextName, ModelReport report, bool warningsAsErrors)
    {
        ReportWriter.Write(output, contextName, report);
        return report.Errors.Count > 0 || (warningsAsErrors && report.Warnings.Count > 0)
            ? ExitCodes.Failed
            : ExitCodes.Passed;
    }

    // Kept out of Run so that a missing Design assembly fails when this method compiles, where Run catches it.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static DbContext CreateContext(Type contextType, Assembly startupAssembly, IReadOnlyList<string> appArguments, TextWriter error)
        => Microsoft.EntityFrameworkCore.Design.DbContextActivator.CreateInstance(
            contextType,
            startupAssembly,
            new Microsoft.EntityFrameworkCore.Design.OperationReportHandler(
                errorHandler: message => error.WriteLine(message),
                warningHandler: message => error.WriteLine(message)),
            [.. appArguments]);
}
