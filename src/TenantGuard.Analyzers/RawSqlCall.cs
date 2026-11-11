// <copyright file="RawSqlCall.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// The calls that take raw SQL, matched by full name so the analyzers depend on neither Dapper nor
// a provider, and the SQL text the compiler can see in one. TG002 checks that text; TG004 reports
// SQL without any. docs/reference/analyzers.md lists the calls.
internal static class RawSqlCall
{
    // The kinds of operation FindSql looks at.
    public static readonly ImmutableArray<OperationKind> Kinds = ImmutableArray.Create(
        OperationKind.Invocation,
        OperationKind.ObjectCreation,
        OperationKind.SimpleAssignment);

    // Namespaces and containing types joined with dots, without generic arguments or global::.
    public static readonly SymbolDisplayFormat FullName = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    private const string EfCoreExtensions = "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions";

    private const string EfCoreQueryableExtensions = "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions";

    // How deep a local's initializer is followed into other locals.
    private const int LocalDepth = 8;

    private const string Dapper = "Dapper.SqlMapper";

    private const string DbCommand = "System.Data.Common.DbCommand";

    private const string IDbCommand = "System.Data.IDbCommand";

    private const string CommandText = "CommandText";

    private static readonly ImmutableHashSet<string> _efCoreMethods = ImmutableHashSet.Create(
        "ExecuteSqlRaw",
        "ExecuteSqlRawAsync",
        "ExecuteSqlInterpolated",
        "ExecuteSqlInterpolatedAsync",
        "ExecuteSql",
        "ExecuteSqlAsync",
        "SqlQueryRaw",
        "SqlQuery");

    private static readonly ImmutableHashSet<string> _fromSqlMethods = ImmutableHashSet.Create("FromSql", "FromSqlRaw", "FromSqlInterpolated");

    private static readonly ImmutableHashSet<string> _dapperMethods = ImmutableHashSet.Create(
        "Query",
        "QueryAsync",
        "QueryFirst",
        "QueryFirstAsync",
        "QueryFirstOrDefault",
        "QueryFirstOrDefaultAsync",
        "QuerySingle",
        "QuerySingleAsync",
        "QuerySingleOrDefault",
        "QuerySingleOrDefaultAsync",
        "QueryMultiple",
        "QueryMultipleAsync",
        "Execute",
        "ExecuteAsync",
        "ExecuteScalar",
        "ExecuteScalarAsync",
        "ExecuteReader",
        "ExecuteReaderAsync",
        "QueryUnbufferedAsync");

    private static readonly ImmutableHashSet<string> _commandTextParameters = ImmutableHashSet.Create("cmdText", "commandText");

    // The SQL handed to a call that takes raw SQL, or null for any other operation: the built-in calls,
    // the tenantguard.raw_sql_methods entries, and FromSql on an entity type with no tenant filter under
    // tenantguard.tenant_members_only.
    public static IOperation? FindSql(IOperation operation, AnalyzerOptions options)
        => operation switch
        {
            IInvocationOperation invocation => FindSql(invocation, options),
            IObjectCreationOperation { Constructor: { } constructor } creation
                when IsCommand(constructor.ContainingType)
                    && constructor.Parameters.Length > 0
                    && constructor.Parameters[0].Type.SpecialType == SpecialType.System_String
                    && _commandTextParameters.Contains(constructor.Parameters[0].Name)
                => Argument(creation.Arguments, constructor.Parameters[0]),
            ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property: { Name: CommandText } property } } assignment
                when IsCommand(property.ContainingType)
                => assignment.Value,
            _ => null,
        };

    // A constant, the text of an interpolated string, with TenantComparison.Hole for each hole that
    // isn't a constant, or the text of a local set once to either. Null when the SQL is built at runtime.
    public static string? KnownText(IOperation sql) => KnownText(sql, LocalDepth);

    private static string? KnownText(IOperation sql, int depth)
    {
        while (sql is IConversionOperation conversion)
        {
            sql = conversion.Operand;
        }

        if (sql.ConstantValue is { HasValue: true, Value: string constant })
        {
            return constant;
        }

        if (sql is ILocalReferenceOperation { Local: var local } && depth > 0)
        {
            return InitializerOf(local, sql.SemanticModel) is { } initializer ? KnownText(initializer, depth - 1) : null;
        }

        // An interpolated string passed as a string, rather than a FormattableString, goes through a handler.
        if (sql is IInterpolatedStringHandlerCreationOperation handler)
        {
            sql = handler.Content;
        }

        if (sql is not (IInterpolatedStringOperation or IInterpolatedStringAdditionOperation))
        {
            return null;
        }

        var text = new StringBuilder();
        AppendKnownText(sql, text);
        return text.ToString();
    }

    private static IOperation? FindSql(IInvocationOperation invocation, AnalyzerOptions options)
    {
        var method = invocation.TargetMethod;
        var type = method.ContainingType.ToDisplayString(FullName);
        if ((type == EfCoreExtensions && _efCoreMethods.Contains(method.Name))
            || (type == Dapper && _dapperMethods.Contains(method.Name)))
        {
            return invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "sql")?.Value;
        }

        // EF Core applies the query filters over FromSql, so it matters only on a type with no tenant
        // filter; otherwise it is checked only when tenantguard.raw_sql_methods lists it.
        if (type == EfCoreQueryableExtensions
            && _fromSqlMethods.Contains(method.Name)
            && !TenantMembers.MayHaveTenantFilter(invocation, options)
            && method.Parameters.Length > 1
            && IsSqlParameter(method.Parameters[1]))
        {
            return Argument(invocation.Arguments, method.Parameters[1]);
        }

        var configured = AnalyzerSettings.RawSqlMethods(options, invocation.Syntax.SyntaxTree);
        if (configured.IsEmpty || !configured.Contains(type + "." + method.Name))
        {
            return null;
        }

        // The first string or FormattableString parameter, after the receiver of an extension method.
        var sqlParameter = method.Parameters.Skip(method.IsExtensionMethod ? 1 : 0).FirstOrDefault(IsSqlParameter);
        return sqlParameter is null ? null : Argument(invocation.Arguments, sqlParameter);
    }

    // What a local's declaration sets it to, when nothing assigns it again, passes it by ref or out, or
    // writes it in a lambda or local function: the member it is declared in is searched for each.
    private static IOperation? InitializerOf(ILocalSymbol local, SemanticModel? model)
    {
        if (model is null
            || local.IsRef
            || local.DeclaringSyntaxReferences.Length != 1
            || local.DeclaringSyntaxReferences[0].GetSyntax() is not VariableDeclaratorSyntax { Initializer.Value: { } value } declarator
            || declarator.SyntaxTree != model.SyntaxTree)
        {
            return null;
        }

        var scope = declarator.Ancestors().FirstOrDefault(node => node is MemberDeclarationSyntax and not GlobalStatementSyntax) ?? declarator.SyntaxTree.GetRoot();
        foreach (var name in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (name.Identifier.ValueText == local.Name
                && IsWritten(name)
                && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, local))
            {
                return null;
            }
        }

        return model.GetOperation(value);
    }

    // An assignment's target, ++ or --, a ref or out argument, a ref expression, or a part of a tuple
    // that is assigned, through parentheses and the null-forgiving !.
    private static bool IsWritten(ExpressionSyntax name)
    {
        // (sql) and sql! are still sql.
        SyntaxNode node = name;
        while (node.Parent is ParenthesizedExpressionSyntax
            || (node.Parent is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression)))
        {
            node = node.Parent;
        }

        switch (node.Parent)
        {
            case AssignmentExpressionSyntax assignment:
                return assignment.Left == node;
            case PrefixUnaryExpressionSyntax prefix:
                return prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression);
            case PostfixUnaryExpressionSyntax postfix:
                return postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression);
            case RefExpressionSyntax:
                return true;
            case ArgumentSyntax argument when argument.Parent is TupleExpressionSyntax tuple:
                return IsWritten(tuple);
            case ArgumentSyntax argument:
                return argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword);
            default:
                return false;
        }
    }

    private static bool IsSqlParameter(IParameterSymbol parameter)
        => parameter.Type.SpecialType == SpecialType.System_String
            || parameter.Type.ToDisplayString(FullName) == "System.FormattableString";

    private static IOperation? Argument(ImmutableArray<IArgumentOperation> arguments, IParameterSymbol parameter)
        => arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == parameter.Ordinal)?.Value;

    // A type that derives from DbCommand or is or implements IDbCommand.
    private static bool IsCommand(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type.ToDisplayString(FullName) == IDbCommand || type.AllInterfaces.Any(candidate => candidate.ToDisplayString(FullName) == IDbCommand))
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString(FullName) == DbCommand)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendKnownText(IOperation interpolated, StringBuilder text)
    {
        if (interpolated is IInterpolatedStringAdditionOperation addition)
        {
            AppendKnownText(addition.Left, text);
            AppendKnownText(addition.Right, text);
        }
        else if (interpolated is IInterpolatedStringOperation parts)
        {
            foreach (var part in parts.Parts)
            {
                if (PartValue(part) is { HasValue: true } constant)
                {
                    // A constant is text, whatever its type: {'T'}enantId is TenantId.
                    text.Append(System.Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    // A hole that isn't a constant is a value, as a parameter is.
                    text.Append(TenantComparison.Hole);
                }
            }
        }
    }

    // A literal part's text, or a hole's value when it is a constant, such as nameof(Invoice.TenantId).
    private static Optional<object?> PartValue(IInterpolatedStringContentOperation part)
        => part switch
        {
            IInterpolatedStringTextOperation literal => literal.Text.ConstantValue,
            IInterpolationOperation hole => hole.Expression.ConstantValue,
            IInterpolatedStringAppendOperation { AppendCall: IInvocationOperation { Arguments.Length: > 0 } append } => append.Arguments[0].Value.ConstantValue,
            _ => default,
        };
}
