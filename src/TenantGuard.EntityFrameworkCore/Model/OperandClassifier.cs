// <copyright file="OperandClassifier.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace TenantGuard;

// SPEC 4.2: classifies the operand a tenant property is compared to by the root of its member and
// call chain.
internal static class OperandClassifier
{
    internal enum Kind
    {
        // A comparison with null, such as e.TenantId != null, is not a tenant comparison.
        Ignored,

        // Rooted at the DbContext instance, which EF parameterizes per query.
        Context,

        // A static member or a static method call.
        Static,

        // A local captured in a compiler-generated closure (TGM003).
        Captured,

        // A primitive, string, Guid or enum constant (TGM004).
        Literal,

        // Anything else (TGM007).
        Unclassified,
    }

    // contextType is the type of the context the model belongs to, or null when it is not known.
    public static Kind Classify(Expression operand, Type? contextType, out string? literal)
    {
        literal = null;
        var start = StripConversions(operand);
        var current = start;
        while (true)
        {
            if (IsContextReference(current, contextType))
            {
                return Kind.Context;
            }

            switch (current)
            {
                case ConstantExpression constant:
                    return ClassifyConstant(constant, isOperand: ReferenceEquals(constant, start), out literal);
                case MemberExpression { Expression: null }:
                case MethodCallExpression { Object: null }:
                    return Kind.Static;
                case MemberExpression member:
                    current = StripConversions(member.Expression);
                    break;
                case MethodCallExpression call:
                    current = StripConversions(call.Object);
                    break;
                default:
                    return Kind.Unclassified;
            }
        }
    }

    public static Expression StripConversions(Expression expression)
    {
        while (expression is UnaryExpression unary && IsConversion(unary.NodeType))
        {
            expression = unary.Operand;
        }

        return expression;
    }

    private static bool IsConversion(ExpressionType nodeType)
        => nodeType is ExpressionType.Convert or ExpressionType.ConvertChecked or ExpressionType.TypeAs or ExpressionType.Quote;

    // In a query filter, EF replaces with the current context any expression, other than an object,
    // whose type the context type derives from or implements: Finbuckle's filter reaches the context
    // through a captured null IMultiTenantDbContext that way. Without the context type, a captured
    // value that is a context, or is null and of a type a context can have, stands in for that test.
    private static bool IsContextReference(Expression expression, Type? contextType)
    {
        if (contextType is not null)
        {
            return expression.Type != typeof(object) && expression.Type.IsAssignableFrom(contextType);
        }

        return expression is MemberExpression { Expression: ConstantExpression { Value: { } closure }, Member: FieldInfo field }
            && field.GetValue(closure) switch
            {
                DbContext => true,
                null => field.FieldType.IsInterface || typeof(DbContext).IsAssignableFrom(field.FieldType),
                _ => false,
            };
    }

    private static Kind ClassifyConstant(ConstantExpression constant, bool isOperand, out string? literal)
    {
        literal = null;
        if (constant.Value is not { } value)
        {
            return isOperand ? Kind.Ignored : Kind.Unclassified;
        }

        var type = value.GetType();
        if (typeof(DbContext).IsAssignableFrom(type))
        {
            return Kind.Context;
        }

        if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            || type.Name.Contains("<>c__DisplayClass", StringComparison.Ordinal))
        {
            return Kind.Captured;
        }

        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(Guid))
        {
            literal = TenantValueFormatter.Format(value);
            return Kind.Literal;
        }

        return Kind.Unclassified;
    }
}
