// <copyright file="Tgm012Models.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.EntityFrameworkCore.Tests.Models;

// TGM012 switchable-filter and TGM013 open-without-tenant: the tenant comparison ORed with a condition
// that doesn't read the row. Each context sets its Invoice filter from Filter.
internal static class Tgm012Models
{
    // Negative TGM012: a flag on the context switches the filter off.
    internal sealed class Switch(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !IsTenantFilterEnabled || row.TenantId == CurrentTenant;
    }

    // Negative TGM012: !(Enabled && TenantId != tenant) is !Enabled || TenantId == tenant.
    internal sealed class NegatedAnd(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !(IsTenantFilterEnabled && row.TenantId != CurrentTenant);
    }

    // Negative TGM012: the switch, compared with true.
    internal sealed class SwitchComparedWithTrue(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => (!IsTenantFilterEnabled || row.TenantId == CurrentTenant) == true;
    }

    // Negative TGM013: the null check, compared with false and negated.
    internal sealed class OpenComparedWithFalse(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !((CurrentTenant == null || row.TenantId == CurrentTenant) == false);
    }

    // Positive: false | x can't switch anything on.
    internal sealed class FalseOr(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => false | (row.TenantId == CurrentTenant);
    }

    // Positive: a condition on the row shows only the rows it selects.
    internal sealed class RowCondition(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => row.IsPublic || row.TenantId == CurrentTenant;
    }

    // Positive: !(BlockAll || TenantId != tenant) is !BlockAll && TenantId == tenant, which only narrows.
    internal sealed class NegatedOr(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !(BlockAll || row.TenantId != CurrentTenant);
    }

    // Positive: the enclosing && rules the switch out.
    internal sealed class GuardedSwitch(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => IsTenantFilterEnabled && (!IsTenantFilterEnabled || row.TenantId == CurrentTenant);
    }

    // Negative TGM013: every row when there's no current tenant.
    internal sealed class OpenWithoutTenant(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => CurrentTenant == null || row.TenantId == CurrentTenant;
    }

    // Negative TGM013: the same, through string.IsNullOrEmpty.
    internal sealed class OpenWhenEmpty(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => string.IsNullOrEmpty(CurrentTenant) || row.TenantId == CurrentTenant;
    }

    // Negative TGM012 and TGM013: a switch and a null check, nested.
    internal sealed class SwitchAndOpen(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !IsTenantFilterEnabled || (CurrentTenant == null || row.TenantId == CurrentTenant);
    }

    // Negative TGM012 and TGM013: the same conditions, grouped the other way.
    internal sealed class SwitchAndOpenRegrouped(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => (CurrentTenant == null || !IsTenantFilterEnabled) || row.TenantId == CurrentTenant;
    }

    // Positive: the enclosing && rules the null check out.
    internal sealed class GuardedNullCheck(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => CurrentTenant != null && (CurrentTenant == null || row.TenantId == CurrentTenant);
    }

    // Positive: !(tenant == null || TenantId != tenant) shows no rows when there's no tenant.
    internal sealed class NegatedNullCheck(DbContextOptions options) : Switches(options)
    {
        protected override Expression<Func<Invoice, bool>> Filter => row => !(CurrentTenant == null || row.TenantId != CurrentTenant);
    }

    // Negative TGM013: a nullable Guid tenant checked with HasValue.
    internal sealed class OpenWithoutGuid(DbContextOptions options) : DbContext(options)
    {
        public Guid? CurrentTenantId { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Ticket>().HasQueryFilter(row => !CurrentTenantId.HasValue || row.TenantId == CurrentTenantId);
    }

    internal abstract class Switches(DbContextOptions options) : DbContext(options)
    {
        public string? CurrentTenant { get; set; } = "acme";

        public bool IsTenantFilterEnabled { get; set; } = true;

        public bool BlockAll { get; set; }

        protected abstract Expression<Func<Invoice, bool>> Filter { get; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Invoice>().HasQueryFilter(Filter);
    }

    internal sealed class Invoice
    {
        public int Id { get; set; }

        public string TenantId { get; set; } = string.Empty;

        public bool IsPublic { get; set; }
    }

    internal sealed class Ticket
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }
    }
}
