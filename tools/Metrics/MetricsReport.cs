// <copyright file="MetricsReport.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Text;

namespace TenantGuard.Tools.Metrics;

// The README's code metrics block: a table per area of the repository, then the most complex
// methods.
internal static class MetricsReport
{
    public const string Start = "<!-- METRICS:START -->";
    public const string End = "<!-- METRICS:END -->";

    // The columns, in order, by the folder their files live under. tools/ isn't counted.
    private static readonly (string Title, string Folder)[] _areas = [("Packages", "src/"), ("Tests", "tests/"), ("Samples", "samples/")];

    public static string Render(IReadOnlyList<FileMetric> files)
    {
        var areas = _areas.Select(area => files.Where(file => file.Path.StartsWith(area.Folder, StringComparison.Ordinal)).ToList()).ToList();
        var counted = areas.SelectMany(area => area).ToList();
        var columns = areas.Append(counted).ToList();

        var text = new StringBuilder();
        text.Append(Start).Append("\n\n");
        text.Append("| Metric | ").AppendJoin(" | ", _areas.Select(area => area.Title)).Append(" | Total |\n");
        text.Append("| --- |").Append(string.Concat(Enumerable.Repeat(" --- |", columns.Count))).Append('\n');
        Row(text, "Files", columns, column => Number(column.Count));
        Row(text, "Lines of code", columns, column => Number(column.Sum(file => file.LinesOfCode)));
        Row(text, "Methods", columns, column => Number(column.Sum(file => file.Methods.Count)));
        Row(text, "Average complexity", columns, column => Methods(column) is { Count: > 0 } methods ? methods.Average(method => method.Complexity).ToString("0.0", CultureInfo.InvariantCulture) : "–");
        Row(text, "Max complexity", columns, column => Methods(column) is { Count: > 0 } methods ? Number(methods.Max(method => method.Complexity)) : "–");

        var complex = Methods(counted)
            .OrderByDescending(method => method.Complexity)
            .ThenBy(method => method.Path, StringComparer.Ordinal)
            .ThenBy(method => method.Line)
            .Take(10);
        text.Append("\n<details>\n<summary>The 10 most complex methods</summary>\n\n");
        text.Append("| Method | Where | Complexity |\n| --- | --- | --- |\n");
        foreach (var method in complex)
        {
            text.Append(CultureInfo.InvariantCulture, $"| `{method.Name}` | `{method.Path}:{method.Line}` | {method.Complexity} |\n");
        }

        text.Append("\n</details>\n\n").Append(End);
        return text.ToString();
    }

    // The document with its metrics block replaced, or null when it has no block.
    public static string? Replace(string document, string block)
    {
        var start = document.IndexOf(Start, StringComparison.Ordinal);
        var end = document.IndexOf(End, StringComparison.Ordinal);
        return start < 0 || end < start ? null : document[..start] + block + document[(end + End.Length)..];
    }

    private static List<MethodMetric> Methods(IEnumerable<FileMetric> files) => [.. files.SelectMany(file => file.Methods)];

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static void Row(StringBuilder text, string metric, IEnumerable<List<FileMetric>> columns, Func<List<FileMetric>, string> value)
        => text.Append("| ").Append(metric).Append(" | ").AppendJoin(" | ", columns.Select(value)).Append(" |\n");
}
