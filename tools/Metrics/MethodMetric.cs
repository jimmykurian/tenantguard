// <copyright file="MethodMetric.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tools.Metrics;

// One method-like member and its cyclomatic complexity.
internal sealed record MethodMetric(string Name, string Path, int Line, int Complexity);
