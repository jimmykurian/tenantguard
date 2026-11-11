// <copyright file="FilterSwitches.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace TenantGuard;

// TGM012 and TGM013: the switches in a tenant filter. Each filter is folded as the other filter rules
// read it, then each ! is pushed inward, so an || counts only when it is still an || with no negation
// above it. A chain of || is one list of conditions, however it is grouped; each condition beside a
// tenant comparison that doesn't read the row is a switch. A null check of the value the tenant
// property is compared with is TGM013, any other switch TGM012. A switch isn't reported when an
// enclosing && holds its opposite, matched by shape.
internal static class FilterSwitches
{
    public static (bool Switchable, bool OpenWithoutTenant) Analyze(IReadOnlyEntityType entityType, IReadOnlyProperty tenantProperty)
    {
        var switchable = false;
        var openWithoutTenant = false;
        foreach (var filter in entityType.GetDeclaredQueryFilters())
        {
            if (filter.Expression is { Parameters.Count: 1 } lambda)
            {
                var walker = new Walker(new TenantAccess(lambda.Parameters[0], tenantProperty), lambda.Parameters[0]);
                walker.Walk(Normalize(QueryFilterAnalysis.Fold(lambda.Body)), []);
                switchable |= walker.Switchable;
                openWithoutTenant |= walker.OpenWithoutTenant;
            }
        }

        return (switchable, openWithoutTenant);
    }

    // TGM005: whether a filter ORs the tenant comparison with a null check of the tenant property, as
    // e => e.TenantId == TenantId || e.TenantId == null does, which lets a row with no tenant through.
    // A null check an enclosing && rules out, as TenantId != null && (... || TenantId == null) does, or
    // one that another declared filter rules out, lets nothing through: EF Core ANDs the filters.
    public static bool LetsNullTenantThrough(IReadOnlyEntityType entityType, IReadOnlyProperty tenantProperty)
    {
        var lambdas = entityType.GetDeclaredQueryFilters()
            .Select(filter => filter.Expression)
            .OfType<LambdaExpression>()
            .Where(lambda => lambda.Parameters.Count == 1)
            .ToList();
        for (var index = 0; index < lambdas.Count; index++)
        {
            var parameter = lambdas[index].Parameters[0];

            // The other filters' conditions, over this filter's parameter, so they compare by shape.
            var others = lambdas.Where((_, other) => other != index)
                .SelectMany(other => Flatten(Normalize(QueryFilterAnalysis.Fold(ReplacingExpressionVisitor.Replace(other.Parameters[0], parameter, other.Body))), IsAnd))
                .ToList();
            var finder = new NullTenantFinder(new TenantAccess(parameter, tenantProperty));
            finder.Walk(Normalize(QueryFilterAnalysis.Fold(lambdas[index].Body)), others);
            if (finder.Found)
            {
                return true;
            }
        }

        return false;
    }

    // Pushes each ! inward over && and ||, reads a bool | or & as || or &&, and reads a comparison with
    // a bool constant as the condition itself or its negation: x == true is x, x == false is !x.
    private static Expression Normalize(Expression expression)
        => OperandClassifier.StripConversions(expression) switch
        {
            UnaryExpression { NodeType: ExpressionType.Not, Method: null } not when not.Type == typeof(bool) => Negate(Normalize(not.Operand)),
            BinaryExpression { Method: null } binary when IsOr(binary) => Expression.OrElse(Normalize(binary.Left), Normalize(binary.Right)),
            BinaryExpression { Method: null } binary when IsAnd(binary) => Expression.AndAlso(Normalize(binary.Left), Normalize(binary.Right)),
            BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual, Method: null } comparison
                when BoolConstantComparison(comparison) is var (condition, value) && condition is not null
                => (comparison.NodeType == ExpressionType.Equal) == value ? Normalize(condition) : Negate(Normalize(condition)),
            _ => expression,
        };

    // The condition and the constant of a comparison between a bool and a bool constant, or nulls.
    private static (Expression? Condition, bool Value) BoolConstantComparison(BinaryExpression comparison)
    {
        if (comparison.Left.Type != typeof(bool) || comparison.Right.Type != typeof(bool))
        {
            return (null, false);
        }

        if (OperandClassifier.StripConversions(comparison.Right) is ConstantExpression { Value: bool right })
        {
            return (comparison.Left, right);
        }

        return OperandClassifier.StripConversions(comparison.Left) is ConstantExpression { Value: bool left } ? (comparison.Right, left) : (null, false);
    }

    // The opposite of a normalized condition, normalized too.
    private static Expression Negate(Expression expression)
    {
        switch (OperandClassifier.StripConversions(expression))
        {
            case BinaryExpression { NodeType: ExpressionType.OrElse } or:
                return Expression.AndAlso(Negate(or.Left), Negate(or.Right));
            case BinaryExpression { NodeType: ExpressionType.AndAlso } and:
                return Expression.OrElse(Negate(and.Left), Negate(and.Right));
            case UnaryExpression { NodeType: ExpressionType.Not, Method: null } not:
                return not.Operand;
            case BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } comparison:
                try
                {
                    return comparison.NodeType == ExpressionType.Equal
                        ? Expression.NotEqual(comparison.Left, comparison.Right, comparison.IsLiftedToNull, null)
                        : Expression.Equal(comparison.Left, comparison.Right, comparison.IsLiftedToNull, null);
                }
                catch (InvalidOperationException)
                {
                    return Expression.Not(expression);
                }

            case ConstantExpression { Value: bool value }:
                return Expression.Constant(!value);
            default:
                return expression.Type == typeof(bool) ? Expression.Not(expression) : expression;
        }
    }

    private static bool IsOr(Expression expression)
        => expression is BinaryExpression { NodeType: ExpressionType.OrElse or ExpressionType.Or, Method: null } binary && binary.Type == typeof(bool);

    private static bool IsAnd(Expression expression)
        => expression is BinaryExpression { NodeType: ExpressionType.AndAlso or ExpressionType.And, Method: null } binary && binary.Type == typeof(bool);

    // The conditions of a chain of the same operator, however it is grouped.
    private static List<Expression> Flatten(Expression expression, Func<Expression, bool> isChain)
    {
        var conditions = new List<Expression>();
        var pending = new Stack<Expression>();
        pending.Push(expression);
        while (pending.Count > 0)
        {
            var next = OperandClassifier.StripConversions(pending.Pop());
            if (isChain(next))
            {
                var binary = (BinaryExpression)next;
                pending.Push(binary.Right);
                pending.Push(binary.Left);
            }
            else
            {
                conditions.Add(next);
            }
        }

        return conditions;
    }

    private static bool IsNull(Expression expression)
        => OperandClassifier.StripConversions(expression) is ConstantExpression { Value: null };

    private static bool Same(Expression left, Expression right)
        => ExpressionEqualityComparer.Instance.Equals(OperandClassifier.StripConversions(left), OperandClassifier.StripConversions(right));

    private sealed class Walker(TenantAccess access, ParameterExpression parameter)
    {
        public bool Switchable { get; private set; }

        public bool OpenWithoutTenant { get; private set; }

        // guards are the conditions of the && chains above the expression.
        public void Walk(Expression expression, IReadOnlyList<Expression> guards)
        {
            if (IsOr(expression))
            {
                var conditions = Flatten(expression, IsOr);
                var comparing = conditions.Where(condition => ComparedOperands(condition).Count > 0).ToList();
                if (comparing.Count > 0)
                {
                    var operands = comparing.SelectMany(ComparedOperands).ToList();

                    // A constant false, such as one a bool | left behind, can't switch anything on.
                    foreach (var condition in conditions.Except(comparing).Where(condition => condition is not ConstantExpression { Value: false }))
                    {
                        if (ReadsRow(condition) || guards.Any(guard => Same(guard, Negate(condition))))
                        {
                            continue;
                        }

                        if (operands.Any(operand => IsNullCheckOf(condition, operand)))
                        {
                            OpenWithoutTenant = true;
                        }
                        else
                        {
                            Switchable = true;
                        }
                    }
                }

                foreach (var condition in comparing)
                {
                    Walk(condition, guards);
                }
            }
            else if (IsAnd(expression))
            {
                var conditions = Flatten(expression, IsAnd);
                for (var index = 0; index < conditions.Count; index++)
                {
                    var others = conditions.Where((_, other) => other != index);
                    Walk(conditions[index], [.. guards, .. others]);
                }
            }
        }

        // x == null, x != null negated, !x.HasValue, string.IsNullOrEmpty(x) or string.IsNullOrWhiteSpace(x).
        private static bool IsNullCheckOf(Expression condition, Expression operand)
            => OperandClassifier.StripConversions(condition) switch
            {
                BinaryExpression { NodeType: ExpressionType.Equal } equal
                    => (IsNull(equal.Right) && Same(equal.Left, operand)) || (IsNull(equal.Left) && Same(equal.Right, operand)),
                UnaryExpression { NodeType: ExpressionType.Not, Operand: MemberExpression { Member.Name: "HasValue", Expression: { } nullable } }
                    => Nullable.GetUnderlyingType(nullable.Type) is not null && Same(nullable, operand),
                MethodCallExpression { Method.Name: nameof(string.IsNullOrEmpty) or nameof(string.IsNullOrWhiteSpace), Arguments: [var argument] } call
                    => call.Method.DeclaringType == typeof(string) && Same(argument, operand),
                _ => false,
            };

        private List<Expression> ComparedOperands(Expression condition)
        {
            var collector = new OperandCollector(access);
            collector.Visit(condition);
            return collector.Operands;
        }

        private bool ReadsRow(Expression condition)
        {
            var finder = new ParameterFinder(parameter);
            finder.Visit(condition);
            return finder.Found;
        }
    }

    // An || chain with a null check of the tenant property among its conditions.
    private sealed class NullTenantFinder(TenantAccess access)
    {
        public bool Found { get; private set; }

        // guards are the conditions of the && chains above the expression, and of the other filters.
        public void Walk(Expression expression, IReadOnlyList<Expression> guards)
        {
            if (IsOr(expression))
            {
                var conditions = Flatten(expression, IsOr);
                Found |= conditions.Any(condition => IsNullCheckOfTenant(condition) && !guards.Any(guard => Same(guard, Negate(condition))));
                foreach (var condition in conditions)
                {
                    Walk(condition, guards);
                }
            }
            else if (IsAnd(expression))
            {
                var conditions = Flatten(expression, IsAnd);
                for (var index = 0; index < conditions.Count; index++)
                {
                    var others = conditions.Where((_, other) => other != index);
                    Walk(conditions[index], [.. guards, .. others]);
                }
            }
        }

        private bool IsNullCheckOfTenant(Expression condition)
            => OperandClassifier.StripConversions(condition) switch
            {
                BinaryExpression { NodeType: ExpressionType.Equal } equal
                    => (IsNull(equal.Right) && access.IsTenantAccess(equal.Left)) || (IsNull(equal.Left) && access.IsTenantAccess(equal.Right)),
                UnaryExpression { NodeType: ExpressionType.Not, Operand: MemberExpression { Member.Name: "HasValue", Expression: { } nullable } }
                    => access.IsTenantAccess(nullable),
                _ => false,
            };
    }

    private sealed class OperandCollector(TenantAccess access) : ExpressionVisitor
    {
        public List<Expression> Operands { get; } = [];

        public override Expression? Visit(Expression? node)
        {
            if (node is not null && access.ComparedOperand(node) is { } operand)
            {
                Operands.Add(operand);
            }

            return base.Visit(node);
        }
    }

    private sealed class ParameterFinder(ParameterExpression parameter) : ExpressionVisitor
    {
        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= node == parameter;
            return node;
        }
    }
}
