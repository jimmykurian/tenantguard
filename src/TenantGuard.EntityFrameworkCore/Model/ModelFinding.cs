// <copyright file="ModelFinding.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard;

/// <summary>
/// One result of a TenantGuard model rule.
/// </summary>
/// <param name="RuleId">The rule identifier, such as <c>TGM001</c>.</param>
/// <param name="Severity">Whether the finding is an error or a warning.</param>
/// <param name="Entity">The display name of the entity type the finding is about.</param>
/// <param name="Property">The tenant property the finding is about, when there is one.</param>
/// <param name="Message">The rule's message, which names the entity, the property and the fix.</param>
public sealed record ModelFinding(string RuleId, Severity Severity, string Entity, string? Property, string Message);
