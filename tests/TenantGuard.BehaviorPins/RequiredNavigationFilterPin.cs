// <copyright file="RequiredNavigationFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// Pin 7 (SPEC 9): a query that reaches a filtered principal through a required navigation joins it with
// an inner join, so dependents of rows the filter hides drop out of the result, a documented caveat.
// https://learn.microsoft.com/ef/core/querying/filters#accessing-entity-with-query-filter-using-required-navigation
public sealed class RequiredNavigationFilterPin
{
    [Fact]
    public async Task Required_navigation_to_a_filtered_principal_drops_its_dependents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);
        var options = new DbContextOptionsBuilder<PinContext>().UseSqlite(connection).Options;

        await using (var setup = new PinContext(options, "acme"))
        {
            await setup.Database.EnsureCreatedAsync(cancellationToken);
            setup.Blogs.AddRange(
                new Blog { Id = 1, TenantId = "acme", Posts = [new Post { Id = 1, Title = "acme-1" }] },
                new Blog { Id = 2, TenantId = "globex", Posts = [new Post { Id = 2, Title = "globex-1" }] });
            await setup.SaveChangesAsync(cancellationToken);
        }

        await using var globex = new PinContext(options, "globex");

        Assert.Equal(2, await globex.Posts.CountAsync(cancellationToken));
        Assert.Equal("globex-1", Assert.Single(await globex.Posts.Include(post => post.Blog).ToListAsync(cancellationToken)).Title);
    }

    private sealed class PinContext(DbContextOptions<PinContext> options, string tenant) : DbContext(options)
    {
        private readonly string _tenant = tenant;

        public DbSet<Blog> Blogs => Set<Blog>();

        public DbSet<Post> Posts => Set<Post>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().HasQueryFilter(blog => blog.TenantId == _tenant);
            modelBuilder.Entity<Post>().HasOne(post => post.Blog).WithMany(blog => blog.Posts).IsRequired();
        }
    }

    private sealed class Blog
    {
        public int Id { get; set; }

        public required string TenantId { get; set; }

        public List<Post> Posts { get; set; } = [];
    }

    private sealed class Post
    {
        public int Id { get; set; }

        public required string Title { get; set; }

        public Blog Blog { get; set; } = null!;
    }
}
