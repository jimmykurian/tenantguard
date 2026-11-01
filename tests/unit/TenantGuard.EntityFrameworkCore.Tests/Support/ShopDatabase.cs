// <copyright file="ShopDatabase.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenantGuard.EntityFrameworkCore.Tests.Models;

namespace TenantGuard.EntityFrameworkCore.Tests.Support;

// One in-memory SQLite database for the Shop model, and contexts on it for any tenant.
internal sealed class ShopDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ShopDatabase()
    {
        _connection.Open();
        using var context = Create(tenant: null);
        context.Database.EnsureCreated();
    }

    // A Shop for the tenant, with CurrentTenant reading Shop.CurrentTenant unless withCurrentTenant
    // is false; configure runs after that, so it can override it.
    public WriteGuardModels.Shop Create(
        string? tenant,
        Action<TenantGuardOptionsBuilder>? configure = null,
        ILoggerFactory? loggerFactory = null,
        bool withCurrentTenant = true)
    {
        var builder = new DbContextOptionsBuilder().UseSqlite(_connection);
        if (loggerFactory is not null)
        {
            builder.UseLoggerFactory(loggerFactory);
        }

        builder.UseTenantGuard(tg =>
        {
            if (withCurrentTenant)
            {
                tg.CurrentTenant(context => ((WriteGuardModels.Shop)context).CurrentTenant);
            }

            configure?.Invoke(tg);
        });
        return new WriteGuardModels.Shop(builder.Options) { CurrentTenant = tenant };
    }

    // Rows past every query filter, read by a context that tracks nothing.
    public List<WriteGuardModels.Invoice> AllInvoices()
    {
        using var context = Create(tenant: null);
        return [.. context.Invoices.IgnoreQueryFilters().AsNoTracking().OrderBy(invoice => invoice.Id)];
    }

    public void Seed(string tenant, params WriteGuardModels.Invoice[] invoices)
    {
        using var context = Create(tenant);
        context.Invoices.AddRange(invoices);
        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();
}
