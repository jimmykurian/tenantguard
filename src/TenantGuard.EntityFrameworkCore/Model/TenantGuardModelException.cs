// <copyright file="TenantGuardModelException.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Text;

namespace TenantGuard;

/// <summary>
/// Thrown while EF builds the model when it breaks a TenantGuard rule with <see cref="Severity.Error"/>
/// severity and <see cref="Enforcement"/> is <see cref="Enforcement.Throw"/>. The message lists every error.
/// </summary>
public sealed class TenantGuardModelException : InvalidOperationException
{
    internal TenantGuardModelException(ModelReport report)
        : base(FormatMessage(report))
    {
        Report = report;
    }

    /// <summary>
    /// Gets the full report, errors and warnings.
    /// </summary>
    public ModelReport Report { get; }

    private static string FormatMessage(ModelReport report)
    {
        var count = report.Errors.Count;
        var message = new StringBuilder();
        message.Append(CultureInfo.InvariantCulture, $"TenantGuard found {count} model {(count == 1 ? "error" : "errors")}:");
        foreach (var error in report.Errors)
        {
            message.Append('\n').Append(CultureInfo.InvariantCulture, $"- {error.RuleId}: {error.Message}");
        }

        return message.ToString();
    }
}
