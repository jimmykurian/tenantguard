// <copyright file="ReportWriter.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// Prints a model report: each finding on its own line, errors first, in the format
/// <see cref="TenantGuardModelException"/> uses, then a summary line.
/// </summary>
internal static class ReportWriter
{
    /// <summary>
    /// Writes the report.
    /// </summary>
    /// <param name="output">Where it goes.</param>
    /// <param name="contextName">The context's type name.</param>
    /// <param name="report">The findings.</param>
    public static void Write(TextWriter output, string contextName, ModelReport report)
    {
        foreach (var finding in report.Errors)
        {
            output.WriteLine($"- {finding.RuleId}: {finding.Message}");
        }

        foreach (var finding in report.Warnings)
        {
            output.WriteLine($"- {finding.RuleId} (warning): {finding.Message}");
        }

        output.WriteLine(
            $"TenantGuard validated {contextName}: {Count(report.Errors.Count, "error")}, {Count(report.Warnings.Count, "warning")}.");
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
