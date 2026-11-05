// <copyright file="ReportingDbContext.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace ValidateTool.Fixture;

// Maps tenant data with no filters, for reports across tenants; its factory declares it CrossTenant.
public sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
}
