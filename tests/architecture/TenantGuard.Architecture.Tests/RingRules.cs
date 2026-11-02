// <copyright file="RingRules.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using NetArchTest.Rules;
using TenantGuard.Architecture.Tests.Support;

namespace TenantGuard.Architecture.Tests;

// The Dependency Rule inside the runtime package: its folders are rings, and a ring depends only
// on rings further in. The public namespace is flat (ADR 0007), so a type's ring is the folder of
// its source file, read from the symbols.
public sealed class RingRules
{
    // Innermost first: configuration, the rules, logging, the write guard, the composition root.
    private static readonly string[] _rings = ["Options", "Model", "Diagnostics", "Interception", "Hosting"];

    // The runtime's source folder in the repository, found from the test output folder.
    private static readonly string _source = FindSource();

    public static TheoryData<string> Rings => [.. _rings];

    [Fact]
    public void Every_runtime_type_with_source_sits_in_a_ring()
    {
        var outside = Types.InAssembly(Assemblies.Runtime).GetTypes()
            .Where(type => type.SourceFilePath is not null && RingOf(type) is null)
            .Select(type => $"{type.FullName} ({type.SourceFilePath})");

        outside.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Rings))]
    public void A_ring_depends_only_on_rings_further_in(string ring)
    {
        var types = Types.InAssembly(Assemblies.Runtime).GetTypes().Where(type => RingOf(type) is not null).ToList();
        var level = Array.IndexOf(_rings, ring);
        var inner = types.Where(type => RingOf(type) == ring).Select(type => type.ReflectionType).ToArray();
        var outer = types.Where(type => Array.IndexOf(_rings, RingOf(type)) > level).Select(type => type.FullName).ToArray();
        inner.Should().NotBeEmpty();
        if (outer.Length == 0)
        {
            return;
        }

        Types.InAssembly(Assemblies.Runtime)
            .That().AreOfType(inner)
            .ShouldNot().HaveDependencyOnAny(outer)
            .GetResult().ShouldPass();
    }

    private static string? RingOf(IType type)
    {
        if (type.SourceFilePath is not { } path)
        {
            return null;
        }

        // A source generator completes partial classes such as TenantGuardLog under obj/; the
        // hand-written half is named after the type (SA1649), and its folder is the ring.
        if (path.Replace('\\', '/').Contains("/obj/", StringComparison.Ordinal))
        {
            path = Directory.GetFiles(_source, type.Name + ".cs", SearchOption.AllDirectories).SingleOrDefault() ?? path;
        }

        return _rings.SingleOrDefault(ring => Path.GetFileName(Path.GetDirectoryName(path)) == ring);
    }

    private static string FindSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TenantGuard.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("TenantGuard.slnx not found above the test output."), "src", "TenantGuard.EntityFrameworkCore");
    }
}
