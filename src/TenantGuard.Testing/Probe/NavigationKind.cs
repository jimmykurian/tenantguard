// <copyright file="NavigationKind.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

namespace TenantGuard.Testing;

// Which way a navigation the NavigationLeak check covers points, which decides the link it seeds.
internal enum NavigationKind
{
    // From the dependent to its principal, such as OrderLine.Order: the link is a new dependent of the
    // viewer's that points at the owner's principal.
    ToPrincipal,

    // From the principal to its dependents, such as Order.Lines: the link is a new dependent of the
    // owner's that points at the viewer's principal.
    ToDependents,

    // A skip navigation of a many-to-many relationship, such as Order.Tags: the link is a join row
    // between the viewer's row and the owner's target row.
    Skip,
}
