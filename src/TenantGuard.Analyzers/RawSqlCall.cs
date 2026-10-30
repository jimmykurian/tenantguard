// <copyright file="RawSqlCall.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TenantGuard.Analyzers;

// The raw SQL methods of SPEC 6, all in RelationalDatabaseFacadeExtensions, and the SQL text the
// compiler can see in a call to one. TG002 checks that text; TG004 reports a call without any.
internal static class RawSqlCall
{
    private static readonly ImmutableHashSet<string> _methodNames = ImmutableHashSet.Create(
        "ExecuteSqlRaw",
        "ExecuteSqlRawAsync",
        "ExecuteSqlInterpolated",
        "ExecuteSqlInterpolatedAsync",
        "ExecuteSql",
        "ExecuteSqlAsync",
        "SqlQueryRaw",
        "SqlQuery");

    public static INamedTypeSymbol? FindExtensions(Compilation compilation)
        => compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions");

    // The SQL handed to a raw SQL method, or null for any other invocation.
    public static IOperation? FindSql(IInvocationOperation invocation, INamedTypeSymbol extensions)
    {
        var method = invocation.TargetMethod;
        if (!_methodNames.Contains(method.Name) || !SymbolEqualityComparer.Default.Equals(method.ContainingType, extensions))
        {
            return null;
        }

        // Parameter 0 is the DatabaseFacade the extension method is called on.
        return invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1)?.Value;
    }

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
