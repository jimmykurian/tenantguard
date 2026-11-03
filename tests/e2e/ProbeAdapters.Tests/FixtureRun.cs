// <copyright file="FixtureRun.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

namespace ProbeAdapters.Tests;

// Runs a built fixture and reads its report: xUnit v3's -xml, or TUnit's TRX.
internal static class FixtureRun
{
    private static readonly XNamespace _trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    public static async Task<IReadOnlyList<TestResult>> XunitAsync()
    {
        var directory = Directory.CreateTempSubdirectory("probe-adapters-");
        var report = Path.Combine(directory.FullName, "results.xml");
        await RunAsync(Metadata("XunitFixture"), "-xml", report);
        return [.. XDocument.Load(report).Descendants("test").Select(FromXunit)];
    }

    public static async Task<IReadOnlyList<TestResult>> TUnitAsync()
    {
        var directory = Directory.CreateTempSubdirectory("probe-adapters-");
        await RunAsync(Metadata("TUnitFixture"), "--report-trx", "--results-directory", directory.FullName);
        var report = XDocument.Load(directory.GetFiles("*.trx").Single().FullName);
        var methods = report.Descendants(_trx + "UnitTest").ToDictionary(
            test => (string)test.Attribute("id")!,
            test => (string)test.Element(_trx + "TestMethod")!.Attribute("name")!);
        return [.. report.Descendants(_trx + "UnitTestResult").Select(result => FromTrx(result, methods))];
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

    // The fixtures fail on purpose, so their exit code isn't checked; the report is.
    private static async Task RunAsync(string fixture, params string[] args)
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

        var cancellationToken = TestContext.Current.CancellationToken;
        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(output, error);
    }

    private static string Metadata(string key)
        => Path.GetFullPath(typeof(FixtureRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value!);
}
