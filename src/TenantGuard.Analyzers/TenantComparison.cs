// <copyright file="TenantComparison.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TenantGuard.Analyzers;

// TG002's check: does the SQL compare a tenant column with a value? The column is a whole identifier,
// bare, quoted or qualified, and passes when compared with = or = ANY (...) to a value on either side,
// followed by IN and a value or a list whose first item is one, or in an INSERT's column list. It reads
// the text; it doesn't parse SQL. docs/reference/analyzers.md gives the grammar.
internal static class TenantComparison
{
    // Stands, in the SQL text, for an interpolated hole that isn't a constant: a value.
    public const char Hole = '';

    private enum Kind
    {
        Name,
        Dot,
        Value,
        Equal,
        Open,
        Close,
        Other,
    }

    public static bool ComparesAny(string sql, ImmutableArray<string> columns)
    {
        var tokens = Tokenize(sql);
        var inserted = InsertedColumns(tokens);
        for (var index = 0; index < tokens.Count; index++)
        {
            if (IsColumn(tokens, index, columns)
                && (inserted.Contains(index) || ComparedAfter(tokens, index) || ComparedBefore(tokens, ChainStart(tokens, index))))
            {
                return true;
            }
        }

        return false;
    }

    // The last part of a name, followed by neither a dot nor a parenthesis, that is one of the columns:
    // TenantId() is a function.
    private static bool IsColumn(List<Token> tokens, int index, ImmutableArray<string> columns)
        => tokens[index].Kind == Kind.Name
            && !Is(tokens, index + 1, Kind.Dot)
            && !Is(tokens, index + 1, Kind.Open)
            && columns.Any(column => string.Equals(column, tokens[index].Text, StringComparison.OrdinalIgnoreCase));

    // The first part of the qualified name that ends at index: i in i.TenantId.
    private static int ChainStart(List<Token> tokens, int index)
    {
        while (index >= 2 && tokens[index - 1].Kind == Kind.Dot && tokens[index - 2].Kind == Kind.Name)
        {
            index -= 2;
        }

        return index;
    }

    // TenantId = value, TenantId = ANY (value, ...), TenantId IN value, TenantId IN (value, ...).
    private static bool ComparedAfter(List<Token> tokens, int index)
    {
        if (Is(tokens, index + 1, Kind.Equal))
        {
            return Is(tokens, index + 2, Kind.Value)
                || (IsWord(tokens, index + 2, "ANY") && Is(tokens, index + 3, Kind.Open) && Is(tokens, index + 4, Kind.Value));
        }

        return IsWord(tokens, index + 1, "IN")
            && (Is(tokens, index + 2, Kind.Value) || (Is(tokens, index + 2, Kind.Open) && Is(tokens, index + 3, Kind.Value)));
    }

    // value = TenantId.
    private static bool ComparedBefore(List<Token> tokens, int start)
        => Is(tokens, start - 1, Kind.Equal) && Is(tokens, start - 2, Kind.Value);

    // The columns named in the list of each INSERT [INTO] table (...).
    private static HashSet<int> InsertedColumns(List<Token> tokens)
    {
        var columns = new HashSet<int>();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (!IsWord(tokens, index, "INSERT"))
            {
                continue;
            }

            var next = IsWord(tokens, index + 1, "INTO") ? index + 2 : index + 1;
            if (!Is(tokens, next, Kind.Name))
            {
                continue;
            }

            next++;
            while (Is(tokens, next, Kind.Dot) && Is(tokens, next + 1, Kind.Name))
            {
                next += 2;
            }

            if (!Is(tokens, next, Kind.Open))
            {
                continue;
            }

            for (next++; next < tokens.Count && tokens[next].Kind != Kind.Close; next++)
            {
                columns.Add(next);
            }
        }

        return columns;
    }

    private static bool Is(List<Token> tokens, int index, Kind kind)
        => index >= 0 && index < tokens.Count && tokens[index].Kind == kind;

    // A bare word, such as IN or ANY, ignoring case; a quoted name is never a keyword.
    private static bool IsWord(List<Token> tokens, int index, string word)
        => Is(tokens, index, Kind.Name) && !tokens[index].Quoted && string.Equals(tokens[index].Text, word, StringComparison.OrdinalIgnoreCase);

    private static List<Token> Tokenize(string sql)
    {
        var tokens = new List<Token>();
        var index = 0;
        while (index < sql.Length)
        {
            var start = index;
            var c = sql[index];
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }

            // A comment is neither a predicate nor a break in one: -- to the end of the line, /* to */.
            if (c == '-' && At(sql, index + 1) == '-')
            {
                index = sql.IndexOf('\n', index) is var lineEnd and >= 0 ? lineEnd + 1 : sql.Length;
                continue;
            }

            if (c == '/' && At(sql, index + 1) == '*')
            {
                index = BlockCommentEnd(sql, index);
                continue;
            }

            if (c == Hole || c == '?')
            {
                index++;
                tokens.Add(new Token(Kind.Value, string.Empty));
            }
            else if (c == '\'' || ((c == 'N' || c == 'n') && At(sql, index + 1) == '\''))
            {
                index = Closing(sql, c == '\'' ? index : index + 1, '\'');
                tokens.Add(new Token(Kind.Value, string.Empty));
            }
            else if (c is '"' or '[' or '`')
            {
                var close = c == '[' ? ']' : c;
                index = Closing(sql, index, close);
                var end = index > start + 1 && sql[index - 1] == close ? index - 1 : index;
                tokens.Add(new Token(Kind.Name, sql.Substring(start + 1, end - start - 1).Replace(new string(close, 2), new string(close, 1)), quoted: true));
            }
            else if (IsNameStart(c))
            {
                index = NameEnd(sql, index);
                tokens.Add(new Token(Kind.Name, sql.Substring(start, index - start)));
            }
            else if (char.IsDigit(c) || (c is '-' or '+' && char.IsDigit(At(sql, index + 1)) && IsUnary(tokens)))
            {
                // A number, signed where a value can start: = -1, IN (-1, +2).
                index++;
                while (index < sql.Length && (char.IsDigit(sql[index]) || sql[index] == '.'))
                {
                    index++;
                }

                tokens.Add(new Token(Kind.Value, string.Empty));
            }
            else if ((c == '@' && IsNameStart(At(sql, index + 1)))
                || (c == ':' && IsNameStart(At(sql, index + 1)))
                || (c == '$' && (IsNameStart(At(sql, index + 1)) || char.IsDigit(At(sql, index + 1)))))
            {
                // A parameter: @name, :name, $name or $1. @@ROWCOUNT and ::int aren't.
                index = NameEnd(sql, index + 1);
                tokens.Add(new Token(Kind.Value, string.Empty));
            }
            else if (c == '{' && Placeholder(sql, index) is var placeholderEnd and > 0)
            {
                // A composite-format placeholder, such as {0}, which ExecuteSqlRaw makes a parameter.
                index = placeholderEnd;
                tokens.Add(new Token(Kind.Value, string.Empty));
            }
            else
            {
                index++;
                tokens.Add(c switch
                {
                    '.' => new Token(Kind.Dot, "."),
                    '(' => new Token(Kind.Open, "("),
                    ')' => new Token(Kind.Close, ")"),
                    '=' when At(sql, index) != '=' => new Token(Kind.Equal, "="),
                    _ => new Token(Kind.Other, Operator(sql, ref index, c)),
                });
            }
        }

        return tokens;
    }

    // A sign here starts a number: at the start, or after =, (, a comma or another operator, or IN.
    private static bool IsUnary(List<Token> tokens)
        => tokens.Count == 0
            || tokens[tokens.Count - 1].Kind is Kind.Equal or Kind.Open or Kind.Other
            || IsWord(tokens, tokens.Count - 1, "IN");

    // The rest of an operator that starts with c, so <=, >=, <>, !=, ==, := and :: aren't read as =.
    private static string Operator(string sql, ref int index, char c)
    {
        if (c is '<' or '>' or '!' or '=' or ':' or '@')
        {
            while (index < sql.Length && sql[index] is '=' or '>' or ':' or '@')
            {
                index++;
            }
        }

        return c.ToString();
    }

    // Past the closing quote, where a doubled quote stands for one; the end of the text if none.
    private static int Closing(string sql, int open, char close)
    {
        var index = open + 1;
        while (index < sql.Length)
        {
            if (sql[index] == close)
            {
                if (At(sql, index + 1) == close)
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index++;
        }

        return index;
    }

    // Past the */ that closes the comment at open, counting nested /* */ as SQL Server and PostgreSQL
    // do; the end of the text if none.
    private static int BlockCommentEnd(string sql, int open)
    {
        var depth = 0;
        var index = open;
        while (index < sql.Length)
        {
            if (sql[index] == '/' && At(sql, index + 1) == '*')
            {
                depth++;
                index += 2;
            }
            else if (sql[index] == '*' && At(sql, index + 1) == '/')
            {
                index += 2;
                if (--depth == 0)
                {
                    return index;
                }
            }
            else
            {
                index++;
            }
        }

        return index;
    }

    // Past {digits}, or 0 when the brace doesn't start one.
    private static int Placeholder(string sql, int open)
    {
        var index = open + 1;
        while (index < sql.Length && char.IsDigit(sql[index]))
        {
            index++;
        }

        return index > open + 1 && At(sql, index) == '}' ? index + 1 : 0;
    }

    // A name goes on with letters, digits, _, $, # and @, as SQL Server's and PostgreSQL's do:
    // TenantId$backup is one name.
    private static int NameEnd(string sql, int index)
    {
        while (index < sql.Length && (char.IsLetterOrDigit(sql[index]) || sql[index] is '_' or '$' or '#' or '@'))
        {
            index++;
        }

        return index;
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    private static char At(string sql, int index) => index < sql.Length ? sql[index] : '\0';

    private readonly struct Token
    {
        public Token(Kind kind, string text, bool quoted = false)
        {
            Kind = kind;
            Text = text;
            Quoted = quoted;
        }

        public Kind Kind { get; }

        public string Text { get; }

        public bool Quoted { get; }
    }
}
