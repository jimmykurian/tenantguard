// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard;

// Hand-rolled tenancy: the app reads the tenant from the X-Tenant header into its own ITenantContext,
// filters every tenant-scoped entity on a member of the context, and registers TenantGuard with startup
// validation. HandRolled.Api.Tests runs the isolation probe against the same model.
var builder = WebApplication.CreateBuilder(args);

var tenantGuardEvents = new TenantGuardEvents();
builder.Logging.AddProvider(tenantGuardEvents);

var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddDbContext<AppDbContext>(options => options.UseAppDatabase(connection));
builder.Services.AddTenantGuardStartupValidation<AppDbContext>();

var app = builder.Build();

// The sample's database lives in memory, so it is created at startup. A real app applies
// migrations as a reviewed deployment step instead, never EnsureCreated or Migrate at startup.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.Use((http, next) =>
{
    http.RequestServices.GetRequiredService<TenantContext>().TenantId = http.Request.Headers["X-Tenant"].FirstOrDefault();
    return next(http);
});

app.MapGet("/customers", (AppDbContext db) => db.Customers.ToListAsync());
app.MapGet("/invoices", (AppDbContext db) => db.Invoices.ToListAsync());

if (args.Contains("--smoke"))
{
    await app.StartAsync();
    await BlockMisStampedInsertAsync(app.Services, tenantGuardEvents);
    await app.StopAsync();
    Console.WriteLine("HandRolled.Api smoke: ok");
    return;
}

await app.RunAsync();

// acme's context inserts an invoice stamped for globex. The write guard throws before the database
// sees it, and logs ViolationBlocked.
static async Task BlockMisStampedInsertAsync(IServiceProvider services, TenantGuardEvents events)
{
    using (var scope = services.CreateScope())
    {
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = "acme";
        var acme = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        acme.Invoices.Add(new Invoice { TenantId = "globex", Number = "globex-1" });
        try
        {
            await acme.SaveChangesAsync();
            throw new InvalidOperationException("Smoke: the mis-stamped insert was saved.");
        }
        catch (TenantIsolationViolationException exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    using (var scope = services.CreateScope())
    {
        if (await CountEveryTenantsInvoicesAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>()) != 0)
        {
            throw new InvalidOperationException("Smoke: the mis-stamped insert reached the database.");
        }
    }

    if (!events.Contains("ViolationBlocked"))
    {
        throw new InvalidOperationException("Smoke: TenantGuard did not log ViolationBlocked.");
    }
}

// Reads past the tenant filter on purpose, so analyzer TG001 asks for the reason it carries.
[AllowCrossTenantAccess("the smoke run checks that no tenant got the blocked invoice")]
static Task<int> CountEveryTenantsInvoicesAsync(AppDbContext db) => db.Invoices.IgnoreQueryFilters().CountAsync();
