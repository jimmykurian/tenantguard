// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;
using TenantGuard.Tools.Metrics;

// task metrics:      prints the metrics block
// task metrics:readme: --write README.md, replacing the README's block
// task metrics:check:  --check README.md, failing while the README's block is out of date
var mode = args.Length > 0 ? args[0] : "--print";
var readme = args.Length > 1 ? args[1] : "README.md";
if (mode is not ("--print" or "--write" or "--check"))
{
    Console.Error.WriteLine("Usage: metrics [--print | --write README.md | --check README.md]");
    return 2;
}

var files = TrackedCSharpFiles()
    .Select(path => CodeMetrics.Measure(path, File.ReadAllText(path)))
    .ToList();
var block = MetricsReport.Render(files);

if (mode == "--print")
{
    Console.WriteLine(block);
    return 0;
}

var document = File.ReadAllText(readme);
if (MetricsReport.Replace(document, block) is not { } updated)
{
    Console.Error.WriteLine($"metrics: {readme} has no {MetricsReport.Start} ... {MetricsReport.End} block.");
    return 1;
}

if (mode == "--write")
{
    File.WriteAllText(readme, updated);
    Console.WriteLine($"metrics: wrote the code metrics into {readme}.");
    return 0;
}

if (updated != document)
{
    Console.Error.WriteLine($"metrics: the code metrics in {readme} are out of date; run task metrics:readme and commit the result.");
    return 1;
}

Console.WriteLine($"metrics: {readme} is up to date.");
return 0;

// The C# files git tracks, from the repository root, in a stable order.
static IEnumerable<string> TrackedCSharpFiles()
{
    using var git = Process.Start(new ProcessStartInfo("git", ["ls-files", "-z", "--", "*.cs"]) { RedirectStandardOutput = true })
        ?? throw new InvalidOperationException("git could not be started.");
    var output = git.StandardOutput.ReadToEnd();
    git.WaitForExit();
    if (git.ExitCode != 0)
    {
        throw new InvalidOperationException($"git ls-files exited with {git.ExitCode}.");
    }

    return output.Split('\0', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal);
}
