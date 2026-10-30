using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

public sealed class SmokeTests
{
    [Fact]
    public async Task Sqlite_context_round_trips_a_row()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<SmokeContext>().UseSqlite(connection).Options;

        await using (var context = new SmokeContext(options))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            context.Invoices.Add(new Invoice { TenantId = "acme", Number = "acme-1" });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = new SmokeContext(options))
        {
            var invoice = await context.Invoices.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("acme", invoice.TenantId);
        }
    }

    private sealed class Invoice
    {
        public int Id { get; set; }
        public required string TenantId { get; set; }
        public required string Number { get; set; }
    }

    private sealed class SmokeContext(DbContextOptions<SmokeContext> options) : DbContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();
    }
}
