// <copyright file="AppDatabase.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard;

// The database options the app and its isolation probe share: SQLite, and TenantGuard reading the
// current tenant from Finbuckle's TenantInfo.
internal static class AppDatabase
{
    public static TBuilder UseAppDatabase<TBuilder>(this TBuilder options, SqliteConnection connection)
        where TBuilder : DbContextOptionsBuilder
    {
        options.UseSqlite(connection).UseTenantGuard(tg => tg.CurrentTenant(context => ((AppDbContext)context).TenantInfo?.Id));
        return options;
    }
}
