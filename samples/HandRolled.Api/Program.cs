// <copyright file="Program.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TenantGuard;

// An in-memory SQLite database, one endpoint, and the TenantGuard write guard. The current tenant
// is a context property; nothing sets it from the request yet.
var builder = WebApplication.CreateBuilder(args);

var tenantGuardEvents = new TenantGuardEvents();
builder.Logging.AddProvider(tenantGuardEvents);

var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
builder.Services.AddSingleton(connection);
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseSqlite(connection)
    .UseTenantGuard(tg => tg.CurrentTenant(context => ((AppDbContext)context).TenantId)));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

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

// acme's context inserts an invoice stamped for globex. The write guard throws before the
// database sees it, and logs ViolationBlocked.
static async Task BlockMisStampedInsertAsync(IServiceProvider services, TenantGuardEvents events)
{
    using (var scope = services.CreateScope())
    {
        var acme = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        acme.TenantId = "acme";
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

internal sealed class Invoice
{
    public int Id { get; set; }
    public required string TenantId { get; set; }
    public required string Number { get; set; }
}

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // The tenant this context reads and writes as.
    public string? TenantId { get; set; }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Invoice>().HasQueryFilter(invoice => invoice.TenantId == TenantId);
}

// Keeps the names of the events TenantGuard logs, for the smoke run.
internal sealed class TenantGuardEvents : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _names = new();

    public bool Contains(string eventName) => _names.Contains(eventName);

    public ILogger CreateLogger(string categoryName)
        => categoryName == "TenantGuard" ? this : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _names.Enqueue(eventId.Name ?? string.Empty);

    public void Dispose()
    {
    }
}
