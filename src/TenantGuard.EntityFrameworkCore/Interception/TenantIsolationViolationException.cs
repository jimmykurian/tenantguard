// <copyright file="TenantIsolationViolationException.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Text;

namespace TenantGuard;

/// <summary>
/// Thrown by <c>SaveChanges</c>, before anything reaches the database, when a write adds, changes,
/// moves or deletes a row outside the current tenant. The message lists up to ten violations and
/// the total.
/// </summary>
public sealed class TenantIsolationViolationException : InvalidOperationException
{
    private const int ListedViolations = 10;

    // details[i] describes the tenant property of violations[i], such as: TenantId is "globex".
    internal TenantIsolationViolationException(IReadOnlyList<TenantViolation> violations, IReadOnlyList<string> details)
        : base(FormatMessage(violations, details))
    {
        Violations = violations;
    }

    // A save through a model built for another tenant, under AllowCapturedValues: no entry is at fault.
    internal TenantIsolationViolationException(string context, object modelTenant, object currentTenant)
        : base(
            $"TenantGuard blocked a save to {context}: its model was built for tenant {TenantValueFormatter.Format(modelTenant)}, "
            + $"so its query filters read {TenantValueFormatter.Format(modelTenant)}'s rows, but the current tenant is {TenantValueFormatter.Format(currentTenant)}. "
            + "Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.")
    {
        Violations = [];
    }

    // A save with a current tenant through the no-tenant model, under CrossTenantWhenNoTenant.
    internal TenantIsolationViolationException(string context, object currentTenant)
        : base(
            $"TenantGuard blocked a save to {context}: its model was built with no tenant and declared cross-tenant, "
            + $"so its query filters read every tenant's rows, but the current tenant is {TenantValueFormatter.Format(currentTenant)}. "
            + "Key the model cache by tenant in IModelCacheKeyFactory, so each tenant gets a model of its own.")
    {
        Violations = [];
    }

    /// <summary>
    /// Gets every blocked write; empty when the save was blocked because the context's model was built
    /// for another tenant.
    /// </summary>
    public IReadOnlyList<TenantViolation> Violations { get; }

    private static string FormatMessage(IReadOnlyList<TenantViolation> violations, IReadOnlyList<string> details)
    {
        var count = violations.Count;
        var writes = count == 1 ? "write" : "writes";

        // The guard reads the current tenant once per save, so every violation expects the same one.
        var currentTenant = violations[0].Expected;
        var message = new StringBuilder(currentTenant is null
            ? $"TenantGuard blocked {count} {writes} because the context has no current tenant:"
            : $"TenantGuard blocked {count} {writes} outside the current tenant {TenantValueFormatter.Format(currentTenant)}:");
        for (var i = 0; i < Math.Min(count, ListedViolations); i++)
        {
            var violation = violations[i];
            message.Append('\n').Append(
                CultureInfo.InvariantCulture,
                $"- {violation.State} {violation.Entity}{FormatKeys(violation.Keys)}: {details[i]}");
        }

        if (count > ListedViolations)
        {
            message.Append('\n').Append(CultureInfo.InvariantCulture, $"- and {count - ListedViolations} more");
        }

        message.Append('\n').Append(currentTenant is null
            ? "Set the current tenant before saving, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason)."
            : "Write only rows of the current tenant, or wrap a write meant to cross tenants in context.TenantGuard().Bypass(reason).");
        return message.ToString();
    }

    private static string FormatKeys(IReadOnlyDictionary<string, object?> keys)
        => keys.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", keys.Select(pair => $"{pair.Key}: {TenantValueFormatter.Format(pair.Value)}"))})";
}
