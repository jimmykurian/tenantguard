// <copyright file="ModelReport.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// The findings of a TenantGuard model validation.
/// </summary>
public sealed class ModelReport
{
    internal ModelReport(IReadOnlyList<ModelFinding> findings)
    {
        Findings = findings;
        Errors = [.. findings.Where(finding => finding.Severity == Severity.Error)];
        Warnings = [.. findings.Where(finding => finding.Severity == Severity.Warning)];
    }

    /// <summary>
    /// Gets every finding, ordered by entity type and then by rule.
    /// </summary>
    public IReadOnlyList<ModelFinding> Findings { get; }

    /// <summary>
    /// Gets the findings with <see cref="Severity.Error"/> severity.
    /// </summary>
    public IReadOnlyList<ModelFinding> Errors { get; }

    /// <summary>
    /// Gets the findings with <see cref="Severity.Warning"/> severity.
    /// </summary>
    public IReadOnlyList<ModelFinding> Warnings { get; }
}
