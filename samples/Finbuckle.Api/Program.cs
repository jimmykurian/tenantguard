using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;

// Skeleton: Finbuckle resolves the tenant from a header against an in-memory store. The
// multi-tenant DbContext and TenantGuard registration arrive in Phase 4.
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

var app = builder.Build();

app.UseMultiTenant();
app.MapGet("/tenant", (HttpContext context) => context.GetMultiTenantContext<TenantInfo>()?.TenantInfo?.Identifier);

if (args.Contains("--smoke"))
{
    await app.StartAsync();
    await app.StopAsync();
    Console.WriteLine("Finbuckle.Api smoke: ok");
    return;
}

await app.RunAsync();

internal static class Tenants
{
    public static readonly string[] All = ["acme", "globex", "initech"];
}
