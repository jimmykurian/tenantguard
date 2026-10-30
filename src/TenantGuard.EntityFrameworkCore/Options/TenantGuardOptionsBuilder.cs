// <copyright file="TenantGuardOptionsBuilder.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;

namespace TenantGuard;

/// <summary>
/// Configures TenantGuard. <c>UseTenantGuard</c> passes one to its callback; create one directly to get
/// <see cref="TenantGuardOptions"/> for <see cref="TenantGuardModel.Validate"/>.
/// </summary>
public sealed class TenantGuardOptionsBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantGuardOptionsBuilder"/> class with the default options.
    /// </summary>
    public TenantGuardOptionsBuilder()
        : this(TenantGuardOptions.Default)
    {
    }

    internal TenantGuardOptionsBuilder(TenantGuardOptions options)
    {
        Options = options;
    }

    /// <summary>
    /// Gets the options configured so far.
    /// </summary>
    public TenantGuardOptions Options { get; private set; }

    /// <summary>
    /// Sets the property names that mark a tenant property by convention, tried in order. The default
    /// is <c>TenantId</c>.
    /// </summary>
    /// <param name="names">One or more property names. CLR and shadow properties both match.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder TenantPropertyNames(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Length == 0)
        {
            throw new ArgumentException("Pass at least one tenant property name.", nameof(names));
        }

        foreach (var name in names)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(names));
        }

        Options = Options.WithTenantPropertyNames([.. names]);
        return this;
    }

    /// <summary>
    /// Names the tenant property of <typeparamref name="TEntity"/>. An explicit registration wins over
    /// <see cref="TenantKeyAttribute"/> and the property names.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="property">The property, such as <c>i =&gt; i.OrgId</c>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder TenantProperty<TEntity>(Expression<Func<TEntity, object?>> property)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(property);

        var body = property.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        if (body is not MemberExpression { Member: PropertyInfo or FieldInfo } member
            || member.Expression != property.Parameters[0])
        {
            throw new ArgumentException(
                $"Pass a property of {typeof(TEntity).Name} itself, such as e => e.TenantId.",
                nameof(property));
        }

        Options = Options.WithTenantProperty(typeof(TEntity), member.Member.Name);
        return this;
    }

    /// <summary>
    /// Requires every entity type to have a tenant property or be declared with <see cref="Shared{TEntity}"/>
    /// (rule TGM002).
    /// </summary>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder RequireAllEntitiesScoped()
    {
        Options = Options.WithRequireAllEntitiesScoped();
        return this;
    }

    /// <summary>
    /// Declares <typeparamref name="TEntity"/> tenant-free on purpose, such as a lookup table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder Shared<TEntity>()
        where TEntity : class
    {
        Options = Options.WithSharedType(typeof(TEntity));
        return this;
    }

    /// <summary>
    /// Sets what happens when the model breaks a rule with <see cref="Severity.Error"/> severity. The
    /// default is <see cref="TenantGuard.Enforcement.Throw"/>.
    /// </summary>
    /// <param name="enforcement">Throw, or log and continue.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder Enforcement(Enforcement enforcement)
    {
        if (!Enum.IsDefined(enforcement))
        {
            throw new ArgumentOutOfRangeException(nameof(enforcement), enforcement, "Unknown enforcement.");
        }

        Options = Options.WithEnforcement(enforcement);
        return this;
    }
}
