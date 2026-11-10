// <copyright file="QueryFilterAnalysis.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TenantGuard;

// Walks an entity type's query filters for references to its tenant property and the operands it
// is compared to.
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
                new Visitor(analysis, lambda.Parameters[0], tenantProperty, contextType).Visit(ConstantFolder.Fold(lambda.Body));
            }
        }

        return analysis;
    }

    // Whether a query filter of the type reaches the tenant property of a tenant-scoped type through
    // navigations, such as line => line.Order.TenantId == TenantId (TGM014).
    public static bool ReachesTenantProperty(IReadOnlyEntityType entityType, TenantGuardOptions options)
    {
        foreach (var filter in entityType.GetDeclaredQueryFilters())
        {
            if (filter.Expression is { Parameters.Count: 1 } lambda)
            {
                var finder = new NavigationTenantFinder(entityType, lambda.Parameters[0], options);
                finder.Visit(lambda.Body);
                if (finder.Found)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The filter's body with its bool constants folded, as every filter rule reads it.
    public static Expression Fold(Expression body) => ConstantFolder.Fold(body);

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

    // Folds the bool constants a filter built by hand can hold, such as Expression.Constant(tenant == null),
    // fixed when the model was built: true || x is true, false || x and true && x are x. A filter folded
    // to true references no tenant property, so it is TGM001. false && x is left as it is.
    private sealed class ConstantFolder : ExpressionVisitor
    {
        public static Expression Fold(Expression body) => new ConstantFolder().Visit(body);

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var visited = base.VisitBinary(node);
            if (visited is not BinaryExpression { NodeType: ExpressionType.OrElse or ExpressionType.AndAlso, Method: null } binary)
            {
                return visited;
            }

            var absorbing = binary.NodeType == ExpressionType.OrElse;
            if (IsConstant(binary.Left, absorbing) || IsConstant(binary.Right, absorbing))
            {
                return absorbing ? Expression.Constant(true) : visited;
            }

            if (IsConstant(binary.Left, !absorbing))
            {
                return binary.Right;
            }

            return IsConstant(binary.Right, !absorbing) ? binary.Left : visited;
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            var visited = base.VisitUnary(node);
            if (visited is UnaryExpression { NodeType: ExpressionType.Not, Method: null } not
                && OperandClassifier.StripConversions(not.Operand) is ConstantExpression { Value: bool value })
            {
                return Expression.Constant(!value);
            }

            return visited;
        }

        private static bool IsConstant(Expression expression, bool value)
            => OperandClassifier.StripConversions(expression) is ConstantExpression { Value: bool constant } && constant == value;
    }

    // Finds a member chain from the parameter through navigations that ends at the tenant property of
    // the type the last navigation reaches.
    private sealed class NavigationTenantFinder(IReadOnlyEntityType entityType, ParameterExpression parameter, TenantGuardOptions options)
        : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            Found |= EndsAtTenantProperty(node);
            return base.VisitMember(node);
        }

        private bool EndsAtTenantProperty(MemberExpression member)
        {
            var names = new Stack<string>();
            Expression? current = member;
            while (OperandClassifier.StripConversions(current!) is MemberExpression step)
            {
                names.Push(step.Member.Name);
                current = step.Expression;
                if (current is null)
                {
                    return false;
                }
            }

            if (OperandClassifier.StripConversions(current!) != parameter || names.Count < 2)
            {
                return false;
            }

            var type = entityType;
            while (names.Count > 1)
            {
                if (type.FindNavigation(names.Pop()) is not { } navigation)
                {
                    return false;
                }

                type = navigation.TargetEntityType;
            }

            return !type.IsOwned() && TenantPropertyDiscovery.Find(type.GetRootType(), options)?.Name == names.Pop();
        }
    }

    private sealed class Visitor(QueryFilterAnalysis analysis, ParameterExpression parameter, IReadOnlyProperty tenantProperty, Type? contextType)
        : ExpressionVisitor
    {
        private readonly TenantAccess _access = new(parameter, tenantProperty);

        protected override Expression VisitMember(MemberExpression node)
        {
            if (_access.IsTenantAccess(node))
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
            if (_access.IsTenantAccess(node))
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

        private void ClassifyOther(Expression left, Expression right)
        {
            if (_access.IsTenantAccess(left))
            {
                analysis.Record(OperandClassifier.Classify(right, contextType, out var literal), literal);
            }
            else if (_access.IsTenantAccess(right))
            {
                analysis.Record(OperandClassifier.Classify(left, contextType, out var literal), literal);
            }
        }
    }
}
