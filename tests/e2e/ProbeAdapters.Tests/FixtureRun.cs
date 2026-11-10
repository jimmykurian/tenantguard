// <copyright file="FixtureRun.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace ProbeAdapters.Tests;

// Runs a built fixture and reads its report: xUnit v3's -xml, or TUnit's TRX. Each fixture runs once
// for the session, and every test reads that run. A fixture whose process crashes leaves no results, so
// it runs again, up to three times: the TUnit fixture on the .NET 10 runtime has crashed on macOS with a
// segmentation fault in the runtime's thread-static lookup, now and then.
internal static class FixtureRun
{
    private const int Attempts = 3;

    private static readonly XNamespace _trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private static readonly Lazy<Task<IReadOnlyList<TestResult>>> _xunit = new(() => RunUntilReportedAsync(XunitOnceAsync));

    private static readonly Lazy<Task<IReadOnlyList<TestResult>>> _tunit = new(() => RunUntilReportedAsync(TUnitOnceAsync));

    public static Task<IReadOnlyList<TestResult>> XunitAsync() => _xunit.Value.WaitAsync(TestContext.Current.CancellationToken);

    public static Task<IReadOnlyList<TestResult>> TUnitAsync() => _tunit.Value.WaitAsync(TestContext.Current.CancellationToken);

    private static async Task<IReadOnlyList<TestResult>> RunUntilReportedAsync(Func<Task<(IReadOnlyList<TestResult> Results, string Output)>> runOnce)
    {
        var outputs = new List<string>();
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var (results, output) = await runOnce();
            if (results.Count > 0)
            {
                return results;
            }

            outputs.Add($"Attempt {attempt}:{Environment.NewLine}{output}");
        }

        throw new InvalidOperationException($"The fixture reported no tests in {Attempts} runs.{Environment.NewLine}{string.Join(Environment.NewLine, outputs)}");
    }

    private static async Task<(IReadOnlyList<TestResult> Results, string Output)> XunitOnceAsync()
    {
        var directory = Directory.CreateTempSubdirectory("probe-adapters-");
        var report = Path.Combine(directory.FullName, "results.xml");
        var output = await RunAsync(Metadata("XunitFixture"), "-xml", report);
        return File.Exists(report) ? ([.. XDocument.Load(report).Descendants("test").Select(FromXunit)], output) : ([], output);
    }

    private static async Task<(IReadOnlyList<TestResult> Results, string Output)> TUnitOnceAsync()
    {
        var directory = Directory.CreateTempSubdirectory("probe-adapters-");
        var output = await RunAsync(Metadata("TUnitFixture"), "--report-trx", "--results-directory", directory.FullName);
        if (directory.GetFiles("*.trx") is not [var file])
        {
            return ([], output);
        }

        var report = XDocument.Load(file.FullName);
        var methods = report.Descendants(_trx + "UnitTest").ToDictionary(
            test => (string)test.Attribute("id")!,
            test => (string)test.Element(_trx + "TestMethod")!.Attribute("name")!);
        return ([.. report.Descendants(_trx + "UnitTestResult").Select(result => FromTrx(result, methods))], output);
    }

    private static TestResult FromXunit(XElement test)
    {
        var outcome = (string)test.Attribute("result")! switch { "Pass" => "Passed", "Fail" => "Failed", _ => "Skipped" };
        var message = (string?)test.Element("failure")?.Element("message") ?? (string?)test.Element("reason") ?? string.Empty;
        return new TestResult((string)test.Attribute("method")!, (string)test.Attribute("name")!, outcome, message);
    }

    // TUnit reports a skipped test as NotExecuted, with its reason in the debug trace.
    private static TestResult FromTrx(XElement result, Dictionary<string, string> methods)
    {
        var outcome = (string)result.Attribute("outcome")! switch { "Passed" => "Passed", "Failed" => "Failed", _ => "Skipped" };
        var message = (string?)result.Descendants(_trx + "Message").FirstOrDefault() ?? (string?)result.Descendants(_trx + "DebugTrace").FirstOrDefault() ?? string.Empty;
        return new TestResult(methods[(string)result.Attribute("testId")!], (string)result.Attribute("testName")!, outcome, message);
    }

    // The fixtures fail on purpose, so their exit code isn't checked; the report is. Returns what the
    // fixture wrote, for a run that reported nothing.
    private static async Task<string> RunAsync(string fixture, params string[] args)
    {
        var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(fixture);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return $"Exit code {process.ExitCode}.{Environment.NewLine}{await output}{await error}";
    }

    private static string Metadata(string key)
        => Path.GetFullPath(typeof(FixtureRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!);
}
