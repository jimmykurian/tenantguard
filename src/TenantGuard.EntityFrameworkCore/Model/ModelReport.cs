// <copyright file="ModelReport.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// The findings of a TenantGuard model validation.
/// </summary>
public sealed class ModelReport
{
    internal ModelReport(
        IReadOnlyList<ModelFinding> findings,
        IReadOnlyList<SuppressedFinding>? suppressed = null,
        IReadOnlyList<Declaration>? unfiltered = null,
        string? crossTenantReason = null)
    {
        Findings = findings;
        Errors = [.. findings.Where(finding => finding.Severity == Severity.Error)];
        Warnings = [.. findings.Where(finding => finding.Severity == Severity.Warning)];
        SuppressedWithReasons = suppressed ?? [];
        Suppressed = [.. SuppressedWithReasons.Select(suppression => suppression.Finding)];
        Unfiltered = unfiltered ?? [];
        CrossTenantReason = crossTenantReason;
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

    /// <summary>
    /// Gets the findings that <c>Suppress</c> left out of <see cref="Findings"/>, ordered as they are.
    /// </summary>
    public IReadOnlyList<ModelFinding> Suppressed { get; }

    // Each suppressed finding with the reason it was suppressed for.
    internal IReadOnlyList<SuppressedFinding> SuppressedWithReasons { get; }

    // The entity types declared Unfiltered, by display name, with their reasons.
    internal IReadOnlyList<Declaration> Unfiltered { get; }

    // The reason the model is cross-tenant, under CrossTenant or CrossTenantWhenNoTenant, when no rule ran.
    internal string? CrossTenantReason { get; }

    internal readonly record struct SuppressedFinding(ModelFinding Finding, string Reason);

    internal readonly record struct Declaration(string Entity, string Reason);
}
