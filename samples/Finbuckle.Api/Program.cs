// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard;

// Finbuckle tenancy: Finbuckle resolves the tenant from the X-Tenant header against an in-memory store
// and filters every IsMultiTenant() entity on its shadow TenantId. TenantGuard checks the model at
// startup and guards writes. Finbuckle.Api.Tests runs the isolation probe against the same model.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant")
    .WithInMemoryStore(options =>
    {
        foreach (var tenant in Tenants.All)
        {
            options.Tenants.Add(new TenantInfo { Id = tenant, Identifier = tenant, Name = tenant });
        }
    });

var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddDbContext<AppDbContext>(options => options.UseAppDatabase(connection));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();

var app = builder.Build();

// The sample's database lives in memory, so it is created at startup. A real app applies
// migrations as a reviewed deployment step instead, never EnsureCreated or Migrate at startup.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseMultiTenant();
app.MapGet("/tenant", (HttpContext context) => context.GetMultiTenantContext<TenantInfo>()?.TenantInfo?.Identifier);
app.MapGet("/invoices", (AppDbContext db) => db.Invoices.ToListAsync());

if (args.Contains("--smoke"))
{
    await app.StartAsync();
    await app.StopAsync();
    Console.WriteLine("Finbuckle.Api smoke: ok");
    return;
}

await app.RunAsync();
