// <copyright file="ReportWriter.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tool;

/// <summary>
/// Prints a model report: each finding on its own line, errors first, in the format
/// <see cref="TenantGuardModelException"/> uses, then the suppressed findings with their reasons, the
/// entity types declared unfiltered, and a summary line. A cross-tenant context gets one line.
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
        if (report.CrossTenantReason is { } reason)
        {
            output.WriteLine($"TenantGuard validated {contextName}: declared cross-tenant ({reason}), so no rules ran.");
            return;
        }

        foreach (var finding in report.Errors)
        {
            output.WriteLine($"- {finding.RuleId}: {finding.Message}");
        }

        foreach (var finding in report.Warnings)
        {
            output.WriteLine($"- {finding.RuleId} (warning): {finding.Message}");
        }

        foreach (var suppressed in report.SuppressedWithReasons)
        {
            output.WriteLine($"- {suppressed.Finding.RuleId} (suppressed: {suppressed.Reason}): {suppressed.Finding.Message}");
        }

        if (report.Unfiltered.Count > 0)
        {
            output.WriteLine(
                $"Unfiltered on purpose: {string.Join(", ", report.Unfiltered.Select(declaration => $"{declaration.Entity} ({declaration.Reason})"))}");
        }

        var suppressedCount = report.Suppressed.Count > 0 ? $", {report.Suppressed.Count} suppressed" : string.Empty;
        output.WriteLine(
            $"TenantGuard validated {contextName}: {Count(report.Errors.Count, "error")}, {Count(report.Warnings.Count, "warning")}{suppressedCount}.");
    }

    private static string Count(int count, string noun) => $"{count} {noun}{(count == 1 ? string.Empty : "s")}";
}
