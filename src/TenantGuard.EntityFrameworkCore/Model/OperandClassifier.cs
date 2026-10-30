// <copyright file="OperandClassifier.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Globalization;
using System.Linq.Expressions;
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

    public static Kind Classify(Expression operand, out string? literal)
    {
        literal = null;
        var start = StripConversions(operand);
        var current = start;
        while (true)
        {
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
            literal = FormatLiteral(value);
            return Kind.Literal;
        }

        return Kind.Unclassified;
    }

    private static string FormatLiteral(object value)
        => value switch
        {
            string text => $"\"{text}\"",
            char character => $"'{character}'",
            bool flag => flag ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
}
