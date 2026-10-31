// <copyright file="ProbeTable.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Text;

namespace TenantGuard.Testing;

// The fixed-width table in TenantIsolationProbeException's message (SPEC 7). The detail column is
// last and not padded.
internal static class ProbeTable
{
    private const string Gap = "  ";

    public static string Format(IReadOnlyList<ProbeRow> rows, int checks)
    {
        string[] header = ["Entity", "Check", "Outcome", "Detail"];
        var cells = rows.Select(row => new[] { row.Entity, row.Check, row.Outcome.ToString(), row.Detail }).ToList();
        var widths = Enumerable.Range(0, 3).Select(column => cells.Prepend(header).Max(cell => cell[column].Length)).ToArray();

        var table = new StringBuilder();
        table.Append(CultureInfo.InvariantCulture, $"TenantGuard isolation probe: {rows.Count} of {checks} checks did not pass.\n\n");
        AppendLine(table, header, widths);
        AppendLine(table, [.. header.Select((title, column) => new string('-', column < 3 ? widths[column] : title.Length))], widths);
        foreach (var cell in cells)
        {
            AppendLine(table, cell, widths);
        }

        return table.ToString().TrimEnd('\n');
    }

    private static void AppendLine(StringBuilder table, string[] cells, int[] widths)
    {
        for (var column = 0; column < 3; column++)
        {
            table.Append(cells[column].PadRight(widths[column])).Append(Gap);
        }

        table.Append(cells[3]).Append('\n');
    }
}
