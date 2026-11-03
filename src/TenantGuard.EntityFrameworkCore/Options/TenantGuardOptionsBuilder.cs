// <copyright file="TenantGuardOptionsBuilder.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

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
    /// Lets a tenant filter capture the tenant when the model cache is keyed by tenant, so each tenant
    /// gets a model of its own (rule TGM003). TGM003 is left out only for a context whose current tenant
    /// is set and whose <c>IModelCacheKeyFactory</c> isn't EF Core's own; the write guard then blocks
    /// a save through a model built for another tenant.
    /// </summary>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder AllowCapturedValues()
    {
        Options = Options.WithAllowCapturedValues();
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

    /// <summary>
    /// Sets how the write guard finds the tenant a context is operating as. The write guard needs it.
    /// </summary>
    /// <param name="resolver">
    /// Returns the context's current tenant, of the tenant property's type, or null when there is none,
    /// such as <c>ctx =&gt; ((AppDbContext)ctx).TenantId</c>.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder CurrentTenant(Func<DbContext, object?> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        Options = Options.WithCurrentTenant(resolver);
        return this;
    }

    /// <summary>
    /// Sets what the write guard does when <c>CurrentTenant</c> returns null. The default is
    /// <see cref="NoTenantPolicy.Reject"/>.
    /// </summary>
    /// <param name="policy">Reject every tenant-scoped write, or allow them and log.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder WhenNoCurrentTenant(NoTenantPolicy policy)
    {
        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown policy.");
        }

        Options = Options.WithNoTenantPolicy(policy);
        return this;
    }

    /// <summary>
    /// Sets whether an added entity whose tenant property is not set, meaning null or the property's
    /// sentinel (its type's default unless set with <c>HasSentinel</c>), gets the current tenant
    /// instead of being rejected. The value is set only when the save has no violations. The default
    /// is off.
    /// </summary>
    /// <param name="stamp">True to stamp the current tenant, false to reject the write.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder StampOnAdd(bool stamp = true)
    {
        Options = Options.WithStampOnAdd(stamp);
        return this;
    }

    /// <summary>
    /// Sets whether each <see cref="TenantViolation"/> carries the entity's primary key values. The
    /// default is on.
    /// </summary>
    /// <param name="include">True to include the key values, false to leave them out.</param>
    /// <returns>The same builder, for chaining.</returns>
    public TenantGuardOptionsBuilder IncludeKeysInErrors(bool include = true)
    {
        Options = Options.WithIncludeKeysInErrors(include);
        return this;
    }
}
