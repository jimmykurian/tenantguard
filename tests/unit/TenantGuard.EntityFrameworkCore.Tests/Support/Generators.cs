// <copyright file="Generators.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using FsCheck;
using FsCheck.Fluent;

namespace TenantGuard.EntityFrameworkCore.Tests.Support;

// FsCheck generators shared by the property tests.
internal static class Generators
{
    // Empty or whitespace-only strings: spaces, tabs, line breaks and a Unicode em space.
    public static Arbitrary<string> Blank()
        => Gen.ListOf(Gen.Elements(' ', '\t', '\n', '\r', ' '))
            .Select(chars => string.Concat(chars))
            .ToArbitrary();

    // Strings with at least one non-whitespace character.
    public static Arbitrary<string> NonBlank()
        => ArbMap.Default.ArbFor<NonWhiteSpaceString>().Convert(value => value.Get, value => NonWhiteSpaceString.NewNonWhiteSpaceString(value));
}
