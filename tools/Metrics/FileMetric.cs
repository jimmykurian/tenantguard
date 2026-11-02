// <copyright file="FileMetric.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Tools.Metrics;

// The metrics of one C# file: its lines of code and its methods.
internal sealed record FileMetric(string Path, int LinesOfCode, IReadOnlyList<MethodMetric> Methods);
