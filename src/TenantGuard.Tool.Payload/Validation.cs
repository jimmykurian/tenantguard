// <copyright file="Validation.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.Tool;

/// <summary>
/// Finds the context, creates it the way <c>dotnet ef</c> does, runs the model rules on it and
/// prints the report.
/// </summary>
internal static class Validation
{
    private const string DesignAssembly = "Microsoft.EntityFrameworkCore.Design";

    private const string FactoryInterface = "Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1";

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

        // The tool prints the model check itself, so TenantGuard doesn't log it too.
        AppContext.SetSwitch(ValidateToolSwitch.Name, true);
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
        catch (Exception exception) when (exception is FileNotFoundException or FileLoadException)
        {
            var depsFile = Path.ChangeExtension(startupAssembly.Location, ".deps.json");
            error.WriteLine(LoadError(exception, startupAssembly.GetName().Name!, contextType!.Name, ListsDesign(depsFile)));
            return ExitCodes.CouldNotValidate;
        }
        catch (Exception exception)
        {
            error.WriteLine(CreateError(exception, contextType!, startupAssembly.GetName().Name!, Factories(startupAssembly)));
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

    // An assembly that didn't load: Design missing from the app, when its .deps.json lists none, or
    // any assembly, Design included, at a version the app doesn't have.
    internal static string LoadError(Exception exception, string startup, string contextName, bool listsDesign)
    {
        var file = exception switch
        {
            FileNotFoundException notFound => notFound.FileName,
            FileLoadException notLoaded => notLoaded.FileName,
            _ => null,
        };
        var name = file is null ? null : new AssemblyName(file).Name;
        return name == DesignAssembly && !listsDesign
            ? $"{startup} doesn't reference {DesignAssembly}. "
                + "Add it to the startup project, as dotnet ef needs, so the tool can create the context the way the app does."
            : $"Couldn't load {name ?? "an assembly"}, which creating {contextName} needs: {exception.Message}";
    }

    // Why creating the context failed, and what to do: more than one design-time factory for a context,
    // which makes EF Core fail for every context; a failure in the context's one factory; or, with no
    // factory, the hint to add one.
    internal static string CreateError(Exception exception, Type contextType, string startup, IReadOnlyList<IGrouping<Type, Type>> factories)
    {
        var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
        if (factories.FirstOrDefault(group => group.Count() > 1) is { } duplicated)
        {
            return $"{startup} has {duplicated.Count()} IDesignTimeDbContextFactory<{duplicated.Key.Name}> types: "
                + $"{string.Join(", ", duplicated.Select(factory => factory.Name).Order(StringComparer.Ordinal))}. "
                + "Keep one, so the tool can create the context the way dotnet ef does.";
        }

        return factories.FirstOrDefault(group => group.Key == contextType) is { } own
            ? $"Couldn't create {contextType.Name} through {own.Single().Name}: {cause.Message}"
            : $"Couldn't create {contextType.Name}: {cause.Message} "
                + $"Add an IDesignTimeDbContextFactory<{contextType.Name}> to the startup assembly, "
                + "or make the app's host build without configuration CI doesn't have.";
    }

    // The startup assembly's design-time factories, by the context each creates.
    internal static List<IGrouping<Type, Type>> Factories(Assembly startupAssembly)
    {
        Type[] types;
        try
        {
            types = startupAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = [.. exception.Types.OfType<Type>()];
        }

        return
        [
            .. types
                .Where(type => type is { IsAbstract: false, IsGenericTypeDefinition: false })
                .SelectMany(type => type.GetInterfaces()
                    .Where(@interface => @interface.IsGenericType && @interface.GetGenericTypeDefinition().FullName == FactoryInterface)
                    .Select(@interface => (Context: @interface.GetGenericArguments()[0], Factory: type)))
                .GroupBy(pair => pair.Context, pair => pair.Factory),
        ];
    }

    // Whether the app's .deps.json lists Microsoft.EntityFrameworkCore.Design.
    internal static bool ListsDesign(string depsFile)
    {
        if (!File.Exists(depsFile))
        {
            return false;
        }

        using var deps = JsonDocument.Parse(File.ReadAllText(depsFile));
        return deps.RootElement.TryGetProperty("libraries", out var libraries)
            && libraries.EnumerateObject().Any(library => library.Name.StartsWith(DesignAssembly + "/", StringComparison.OrdinalIgnoreCase));
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
