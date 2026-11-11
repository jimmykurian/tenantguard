// <copyright file="AnalyzerSettings.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TenantGuard.Analyzers;

// The analyzers' .editorconfig keys, read for the file being analyzed. Each takes a comma-separated list,
// except tenant_members_only and tenant_filter_unnamed, which take true or false.
internal static class AnalyzerSettings
{
    public const string TenantColumnKey = "tenantguard.tenant_column";

    public const string TenantFilterNamesKey = "tenantguard.tenant_filter_names";

    public const string RawSqlMethodsKey = "tenantguard.raw_sql_methods";

    public const string TenantMembersOnlyKey = "tenantguard.tenant_members_only";

    public const string TenantFilterUnnamedKey = "tenantguard.tenant_filter_unnamed";

    public const string FilterSwitchMethodsKey = "tenantguard.filter_switch_methods";

    private static readonly ImmutableArray<string> _defaultTenantColumns = ImmutableArray.Create("TenantId");

    private static readonly char[] _separator = [','];

    // The columns raw SQL has to mention: TenantId unless set.
    public static ImmutableArray<string> TenantColumns(AnalyzerOptions options, SyntaxTree tree)
    {
        var columns = Read(options, tree, TenantColumnKey);
        return columns.IsEmpty ? _defaultTenantColumns : columns;
    }

    // Whether tenantguard.tenant_column names at least one column.
    public static bool TenantColumnIsSet(AnalyzerOptions options, SyntaxTree tree) => !Read(options, tree, TenantColumnKey).IsEmpty;

    // The names of the tenant query filters, or none when not set.
    public static ImmutableArray<string> TenantFilterNames(AnalyzerOptions options, SyntaxTree tree)
        => Read(options, tree, TenantFilterNamesKey);

    // More methods that take raw SQL, as Namespace.Type.Method, or none when not set.
    public static ImmutableArray<string> RawSqlMethods(AnalyzerOptions options, SyntaxTree tree)
        => Read(options, tree, RawSqlMethodsKey);

    // More methods that turn the tenant filter off, as Namespace.Type.Method with an optional
    // <Namespace.TypeArgument>, or none when not set.
    public static ImmutableArray<string> FilterSwitchMethods(AnalyzerOptions options, SyntaxTree tree)
        => Read(options, tree, FilterSwitchMethodsKey);

    // Whether every tenant property is a member of its entity's class, never a shadow property:
    // true only when set to true.
    public static bool TenantMembersOnly(AnalyzerOptions options, SyntaxTree tree) => IsTrue(options, tree, TenantMembersOnlyKey);

    // Whether the tenant filters have no name: true only when set to true.
    public static bool TenantFilterUnnamed(AnalyzerOptions options, SyntaxTree tree) => IsTrue(options, tree, TenantFilterUnnamedKey);

    private static bool IsTrue(AnalyzerOptions options, SyntaxTree tree, string key)
        => options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue(key, out var value)
            && string.Equals(value.Trim(), "true", System.StringComparison.OrdinalIgnoreCase);

    private static ImmutableArray<string> Read(AnalyzerOptions options, SyntaxTree tree, string key)
        => options.AnalyzerConfigOptionsProvider.GetOptions(tree).TryGetValue(key, out var value)
            ? value.Split(_separator).Select(item => item.Trim()).Where(item => item.Length > 0).ToImmutableArray()
            : ImmutableArray<string>.Empty;
}
