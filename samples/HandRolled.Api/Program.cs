// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

// Skeleton: an in-memory SQLite database and one endpoint. Tenant resolution, query filters
// and TenantGuard registration arrive in Phase 4.
var builder = WebApplication.CreateBuilder(args);

var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.MapGet("/invoices", (AppDbContext db) => db.Invoices.ToListAsync());

if (args.Contains("--smoke"))
{
    await app.StartAsync();
    await app.StopAsync();
    Console.WriteLine("HandRolled.Api smoke: ok");
    return;
}

await app.RunAsync();

internal sealed class Invoice
{
    public int Id { get; set; }
    public required string TenantId { get; set; }
    public required string Number { get; set; }
}

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
}
