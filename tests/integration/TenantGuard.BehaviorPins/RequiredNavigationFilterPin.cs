// <copyright file="RequiredNavigationFilterPin.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using LightBDD.Framework;
using LightBDD.Framework.Scenarios;
using LightBDD.XUnit3;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard.BehaviorPins;

// https://learn.microsoft.com/ef/core/querying/filters#accessing-entity-with-query-filter-using-required-navigation
[FeatureDescription("A query that reaches a filtered principal through a required navigation joins it with an inner join, so dependents of rows the filter hides drop out of the result, a documented caveat.")]
public sealed class RequiredNavigationFilterPin : FeatureFixture, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private DbContextOptions<PinContext> _options = null!;
    private int _postCount;
    private List<Post> _postsWithBlog = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task Required_navigation_to_a_filtered_principal_drops_its_dependents()
        => Runner.RunScenarioAsync(
            Given_acme_and_globex_each_have_a_blog_with_a_post,
            When_globex_reads_the_posts_with_and_without_their_blog,
            Then_globex_counts_both_posts,
            Then_including_the_blog_drops_acmes_post);

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    private async Task Given_acme_and_globex_each_have_a_blog_with_a_post()
    {
        await _connection.OpenAsync(Token);
        _options = new DbContextOptionsBuilder<PinContext>().UseSqlite(_connection).Options;
        await using var setup = new PinContext(_options, "acme");
        await setup.Database.EnsureCreatedAsync(Token);
        setup.Blogs.AddRange(
            new Blog { Id = 1, TenantId = "acme", Posts = [new Post { Id = 1, Title = "acme-1" }] },
            new Blog { Id = 2, TenantId = "globex", Posts = [new Post { Id = 2, Title = "globex-1" }] });
        await setup.SaveChangesAsync(Token);
    }

    private async Task When_globex_reads_the_posts_with_and_without_their_blog()
    {
        await using var globex = new PinContext(_options, "globex");
        _postCount = await globex.Posts.CountAsync(Token);
        _postsWithBlog = await globex.Posts.Include(post => post.Blog).ToListAsync(Token);
    }

    private Task Then_globex_counts_both_posts()
    {
        _postCount.Should().Be(2);
        return Task.CompletedTask;
    }

    private Task Then_including_the_blog_drops_acmes_post()
    {
        _postsWithBlog.Should().ContainSingle().Subject.Title.Should().Be("globex-1");
        return Task.CompletedTask;
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
