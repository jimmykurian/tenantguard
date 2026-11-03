// <copyright file="RawSqlCall.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
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

    private const string EfCoreExtensions = "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions";

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

    // Namespaces and containing types joined with dots, without generic arguments or global::.
    private static readonly SymbolDisplayFormat _fullName = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    // The SQL handed to a call that takes raw SQL, or null for any other operation. configured holds
    // the tenantguard.raw_sql_methods entries.
    public static IOperation? FindSql(IOperation operation, ImmutableArray<string> configured)
        => operation switch
        {
            IInvocationOperation invocation => FindSql(invocation, configured),
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

    // A constant, or the text and constant holes of an interpolated string. Null when the SQL is
    // built at runtime.
    public static string? KnownText(IOperation sql)
    {
        while (sql is IConversionOperation conversion)
        {
            sql = conversion.Operand;
        }

        if (sql.ConstantValue is { HasValue: true, Value: string constant })
        {
            return constant;
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

    private static IOperation? FindSql(IInvocationOperation invocation, ImmutableArray<string> configured)
    {
        var method = invocation.TargetMethod;
        var type = method.ContainingType.ToDisplayString(_fullName);
        if ((type == EfCoreExtensions && _efCoreMethods.Contains(method.Name))
            || (type == Dapper && _dapperMethods.Contains(method.Name)))
        {
            return invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "sql")?.Value;
        }

        if (configured.IsEmpty || !configured.Contains(type + "." + method.Name))
        {
            return null;
        }

        // The first string or FormattableString parameter, after the receiver of an extension method.
        var sqlParameter = method.Parameters.Skip(method.IsExtensionMethod ? 1 : 0).FirstOrDefault(IsSqlParameter);
        return sqlParameter is null ? null : Argument(invocation.Arguments, sqlParameter);
    }

    private static bool IsSqlParameter(IParameterSymbol parameter)
        => parameter.Type.SpecialType == SpecialType.System_String
            || parameter.Type.ToDisplayString(_fullName) == "System.FormattableString";

    private static IOperation? Argument(ImmutableArray<IArgumentOperation> arguments, IParameterSymbol parameter)
        => arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == parameter.Ordinal)?.Value;

    // A type that derives from DbCommand or is or implements IDbCommand.
    private static bool IsCommand(INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return false;
        }

        if (type.ToDisplayString(_fullName) == IDbCommand || type.AllInterfaces.Any(candidate => candidate.ToDisplayString(_fullName) == IDbCommand))
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString(_fullName) == DbCommand)
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
                if (PartValue(part) is { HasValue: true, Value: string piece })
                {
                    text.Append(piece);
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
