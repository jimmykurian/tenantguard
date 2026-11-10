// <copyright file="TenantAccess.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Tells a read of the tenant property in a query filter: e.TenantId, e.Blog.TenantId through a
// navigation, or EF.Property<T>(e, "TenantId"), with the name a constant or a captured string.
internal sealed class TenantAccess(ParameterExpression parameter, IReadOnlyProperty tenantProperty)
{
    // EF.Property<T>(instance, name), with the name a constant or a captured string.
    public static bool IsEfProperty(MethodCallExpression call, out Expression instance, out string? name)
    {
        if (call.Method.DeclaringType == typeof(EF)
            && call.Method.Name == nameof(EF.Property)
            && call.Arguments.Count == 2)
        {
            instance = call.Arguments[0];
            name = OperandClassifier.EvaluateFixedValue(call.Arguments[1]) as string;
            return name is not null;
        }

        instance = call;
        name = null;
        return false;
    }

    public bool IsTenantAccess(Expression expression)
        => OperandClassifier.StripConversions(expression) switch
        {
            MemberExpression member => IsTenantMember(member.Member) && IsRootedAtParameter(member.Expression),
            MethodCallExpression call => IsEfProperty(call, out var instance, out var name)
                && name == tenantProperty.Name
                && IsRootedAtParameter(instance),
            _ => false,
        };

    // The operand a comparison of the tenant property compares it with, or null when the comparison
    // isn't one: ==, !=, Equals or Contains with the tenant property on one side.
    public Expression? ComparedOperand(Expression expression)
    {
        switch (expression)
        {
            case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } binary:
                return Other(binary.Left, binary.Right);
            case MethodCallExpression { Method.Name: nameof(object.Equals) or nameof(Enumerable.Contains) } call:
                var operands = call.Object is null ? [.. call.Arguments] : new List<Expression> { call.Object };
                if (call.Object is not null)
                {
                    operands.AddRange(call.Arguments);
                }

                return operands.Count >= 2 ? Other(operands[0], operands[1]) : null;
            default:
                return null;
        }
    }

    public bool IsRootedAtParameter(Expression? expression)
    {
        while (expression is not null)
        {
            switch (OperandClassifier.StripConversions(expression))
            {
                case ParameterExpression root:
                    return root == parameter;
                case MemberExpression member:
                    expression = member.Expression;
                    break;
                case MethodCallExpression call when IsEfProperty(call, out var instance, out _):
                    expression = instance;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    // A comparison with null isn't a tenant comparison.
    private static bool IsNull(Expression expression)
        => OperandClassifier.StripConversions(expression) is ConstantExpression { Value: null };

    private Expression? Other(Expression left, Expression right)
    {
        if (IsTenantAccess(left) && !IsNull(right))
        {
            return right;
        }

        return IsTenantAccess(right) && !IsNull(left) ? left : null;
    }

    private bool IsTenantMember(MemberInfo member)
        => member.Name == tenantProperty.Name
            || member == tenantProperty.PropertyInfo
            || member == tenantProperty.FieldInfo;
}
