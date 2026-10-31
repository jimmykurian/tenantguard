// <copyright file="QueryFilterAnalysis.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Walks an entity type's query filters for references to its tenant property and the operands it
// is compared to (SPEC 4.2).
internal sealed class QueryFilterAnalysis
{
    private QueryFilterAnalysis()
    {
    }

    public bool ReferencesTenantProperty { get; private set; }

    public bool HasComparison { get; private set; }

    public bool CapturesLocal { get; private set; }

    public bool HasUnclassifiedOperand { get; private set; }

    public List<string> Literals { get; } = [];

    public static QueryFilterAnalysis Analyze(IReadOnlyEntityType entityType, IReadOnlyProperty tenantProperty, Type? contextType)
    {
        var analysis = new QueryFilterAnalysis();
        foreach (var filter in entityType.GetDeclaredQueryFilters())
        {
            if (filter.Expression is { Parameters.Count: 1 } lambda)
            {
                new Visitor(analysis, lambda.Parameters[0], tenantProperty, contextType).Visit(lambda.Body);
            }
        }

        return analysis;
    }

    private void Record(OperandClassifier.Kind kind, string? literal)
    {
        if (kind == OperandClassifier.Kind.Ignored)
        {
            return;
        }

        HasComparison = true;
        switch (kind)
        {
            case OperandClassifier.Kind.Captured:
                CapturesLocal = true;
                break;
            case OperandClassifier.Kind.Literal when literal is not null && !Literals.Contains(literal):
                Literals.Add(literal);
                break;
            case OperandClassifier.Kind.Unclassified:
                HasUnclassifiedOperand = true;
                break;
        }
    }

    private sealed class Visitor(QueryFilterAnalysis analysis, ParameterExpression parameter, IReadOnlyProperty tenantProperty, Type? contextType)
        : ExpressionVisitor
    {
        protected override Expression VisitMember(MemberExpression node)
        {
            if (IsTenantAccess(node))
            {
                analysis.ReferencesTenantProperty = true;
            }

            return base.VisitMember(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                ClassifyOther(node.Left, node.Right);
            }

            return base.VisitBinary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsTenantAccess(node))
            {
                analysis.ReferencesTenantProperty = true;
            }
            else if (node.Method.Name is nameof(object.Equals) or nameof(Enumerable.Contains))
            {
                var operands = node.Object is null ? [.. node.Arguments] : new List<Expression> { node.Object };
                if (node.Object is not null)
                {
                    operands.AddRange(node.Arguments);
                }

                if (operands.Count >= 2)
                {
                    ClassifyOther(operands[0], operands[1]);
                }
            }

            return base.VisitMethodCall(node);
        }

        private static bool IsEfProperty(MethodCallExpression call, out Expression instance, out string? name)
        {
            if (call.Method.DeclaringType == typeof(EF)
                && call.Method.Name == nameof(EF.Property)
                && call.Arguments.Count == 2)
            {
                instance = call.Arguments[0];
                name = (OperandClassifier.StripConversions(call.Arguments[1]) as ConstantExpression)?.Value as string;
                return name is not null;
            }

            instance = call;
            name = null;
            return false;
        }

        private void ClassifyOther(Expression left, Expression right)
        {
            if (IsTenantAccess(left))
            {
                analysis.Record(OperandClassifier.Classify(right, contextType, out var literal), literal);
            }
            else if (IsTenantAccess(right))
            {
                analysis.Record(OperandClassifier.Classify(left, contextType, out var literal), literal);
            }
        }

        // e.TenantId, e.Blog.TenantId through a navigation, or EF.Property<T>(e, "TenantId").
        private bool IsTenantAccess(Expression expression)
            => OperandClassifier.StripConversions(expression) switch
            {
                MemberExpression member => IsTenantMember(member.Member) && IsRootedAtParameter(member.Expression),
                MethodCallExpression call => IsEfProperty(call, out var instance, out var name)
                    && name == tenantProperty.Name
                    && IsRootedAtParameter(instance),
                _ => false,
            };

        private bool IsTenantMember(MemberInfo member)
            => member.Name == tenantProperty.Name
                || member == tenantProperty.PropertyInfo
                || member == tenantProperty.FieldInfo;

        private bool IsRootedAtParameter(Expression? expression)
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
    }
}
