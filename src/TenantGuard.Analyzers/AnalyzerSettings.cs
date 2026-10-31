// <copyright file="AnalyzerSettings.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// The analyzers' .editorconfig keys, read for the file being analyzed. Both take a comma-separated list.
internal static class AnalyzerSettings
{
    public const string TenantColumnKey = "tenantguard.tenant_column";

    public const string TenantFilterNamesKey = "tenantguard.tenant_filter_names";

    private static readonly ImmutableArray<string> _defaultTenantColumns = ImmutableArray.Create("TenantId");

    private static readonly char[] _separator = [','];

    // The columns raw SQL has to mention: TenantId unless set.
    public static ImmutableArray<string> TenantColumns(AnalyzerOptions options, SyntaxTree tree)
    {
        var columns = Read(options, tree, TenantColumnKey);
        return columns.IsEmpty ? _defaultTenantColumns : columns;
    }

    // The names of the tenant query filters, or none when not set.
    public static ImmutableArray<string> TenantFilterNames(AnalyzerOptions options, SyntaxTree tree)
        => Read(options, tree, TenantFilterNamesKey);

    private static ImmutableArray<string> Read(AnalyzerOptions options, SyntaxTree tree, string key)
        => options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue(key, out var value)
            ? value.Split(_separator).Select(item => item.Trim()).Where(item => item.Length > 0).ToImmutableArray()
            : ImmutableArray<string>.Empty;
}
