// <copyright file="CallSite.cs" company="Jimmy Kurian">
// Copyright (c) Jimmy Kurian 2026. Licensed under the Apache License, Version 2.0.
// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace TenantGuard.Analyzers;

// Where TG001 and TG005 underline a call.
internal static class CallSite
{
    // From the method name to the closing parenthesis, so a long query chain is not underlined.
    public static Location Of(IInvocationOperation invocation)
    {
        if (invocation.Syntax is not InvocationExpressionSyntax call)
        {
            return invocation.Syntax.GetLocation();
        }

        var name = call.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            var expression => expression,
        };
        return Location.Create(call.SyntaxTree, TextSpan.FromBounds(name.SpanStart, call.Span.End));
    }
}
