// <copyright file="EntityQueries.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard.Testing;

// The queries the checks run through Set<TEntity>(), for an entity type the probe knows only at runtime.
internal sealed class EntityQueries
{
    private readonly Func<DbContext, CancellationToken, Task<List<object>>> _list;
    private readonly Func<DbContext, object?[], CancellationToken, ValueTask<object?>> _find;
    private readonly Func<DbContext, object?[], CancellationToken, Task<int>> _executeDelete;
    private readonly Func<DbContext, object?[], string, CancellationToken, Task<object?>> _include;

    private EntityQueries(
        Func<DbContext, CancellationToken, Task<List<object>>> list,
        Func<DbContext, object?[], CancellationToken, ValueTask<object?>> find,
        Func<DbContext, object?[], CancellationToken, Task<int>> executeDelete,
        Func<DbContext, object?[], string, CancellationToken, Task<object?>> include)
    {
        _list = list;
        _find = find;
        _executeDelete = executeDelete;
        _include = include;
    }

    public static EntityQueries For(IEntityType type)
        => (EntityQueries)typeof(EntityQueries)
            .GetMethod(nameof(Create), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type.ClrType)
            .Invoke(null, [type.FindPrimaryKey()!])!;

    // Every row of the entity type the context can see, tracked.
    public Task<List<object>> ListAsync(DbContext context, CancellationToken cancellationToken) => _list(context, cancellationToken);

    public ValueTask<object?> FindAsync(DbContext context, object?[] keyValues, CancellationToken cancellationToken)
        => _find(context, keyValues, cancellationToken);

    // ExecuteDelete for the row with the key, which deletes nothing the context cannot see.
    public Task<int> ExecuteDeleteAsync(DbContext context, object?[] keyValues, CancellationToken cancellationToken)
        => _executeDelete(context, keyValues, cancellationToken);

    // The row with the key, with Include of the navigation, or null when the context cannot see it.
    public Task<object?> IncludeAsync(DbContext context, object?[] keyValues, string navigation, CancellationToken cancellationToken)
        => _include(context, keyValues, navigation, cancellationToken);

    private static EntityQueries Create<TEntity>(IKey key)
        where TEntity : class
        => new(
            async (context, cancellationToken) => [.. await context.Set<TEntity>().ToListAsync(cancellationToken)],
            async (context, keyValues, cancellationToken) => await context.Set<TEntity>().FindAsync(keyValues, cancellationToken),
            (context, keyValues, cancellationToken) => context.Set<TEntity>().Where(HasKey<TEntity>(key, keyValues)).ExecuteDeleteAsync(cancellationToken),
            async (context, keyValues, navigation, cancellationToken)
                => await context.Set<TEntity>().Where(HasKey<TEntity>(key, keyValues)).Include(navigation).FirstOrDefaultAsync(cancellationToken));

    // row => EF.Property<TKey>(row, "Id") == keyValue, for each key property.
    private static Expression<Func<TEntity, bool>> HasKey<TEntity>(IKey key, object?[] keyValues)
    {
        var row = Expression.Parameter(typeof(TEntity), "row");
        var body = key.Properties
            .Select((property, index) => (Expression)Expression.Equal(
                Expression.Call(typeof(EF), nameof(EF.Property), [property.ClrType], row, Expression.Constant(property.Name)),
                Expression.Constant(keyValues[index], property.ClrType)))
            .Aggregate(Expression.AndAlso);
        return Expression.Lambda<Func<TEntity, bool>>(body, row);
    }
}
