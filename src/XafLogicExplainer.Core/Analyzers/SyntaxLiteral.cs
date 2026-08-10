using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Reads the value a syntax expression stands for, without a compilation.
/// </summary>
/// <remarks>
/// Extraction is syntax-only, so there is no semantic model to ask "what does this evaluate to".
/// The analyzers previously approximated it with <c>expression.ToString().Trim('"')</c>, which is
/// right for a plain string literal and wrong for everything else.
/// <para>
/// It is wrong most visibly for <c>nameof</c>. <c>[XafDefaultProperty(nameof(Numero))]</c> is how
/// current C# is written — refactoring-safe, and what an IDE generates — and the naive read
/// produced the literal text <c>nameof(Numero)</c> as the property name. That value then flowed
/// into generated documentation and MCP responses as though it were real, which is the failure
/// mode this project exists to prevent.
/// </para>
/// </remarks>
public static class SyntaxLiteral
{
    /// <summary>
    /// Returns the value an expression denotes: the text of a string literal, the identifier a
    /// <c>nameof</c> refers to, or the source text for anything else.
    /// </summary>
    /// <param name="expression">The expression to read.</param>
    public static string ValueOf(ExpressionSyntax expression)
    {
        switch (expression)
        {
            // "text"
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;

            // nameof(Total) -> Total, and nameof(Invoice.Total) -> Total, because the attribute
            // means the member name in both cases.
            case InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.Text: "nameof" } } invocation
                when invocation.ArgumentList.Arguments.Count == 1:
            {
                var argument = invocation.ArgumentList.Arguments[0].Expression;
                return argument switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.Text,
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                    _ => argument.ToString(),
                };
            }

            // "a" + "b", used for long validation messages split across lines.
            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                return ValueOf(binary.Left) + ValueOf(binary.Right);

            // Enum members, constants, numbers, typeof(...) and anything else: the source text is
            // the most faithful thing available without a compilation. Quotes are still trimmed so
            // behavior matches the previous implementation for the cases it handled correctly.
            default:
                return expression.ToString().Trim('"');
        }
    }

    /// <summary>
    /// Reads an expression that may be absent.
    /// </summary>
    /// <param name="expression">The expression, or null.</param>
    public static string? ValueOrNull(ExpressionSyntax? expression) =>
        expression is null ? null : ValueOf(expression);

    /// <summary>
    /// Reads an attribute argument that may be absent.
    /// </summary>
    /// <param name="argument">The argument, or null.</param>
    public static string? ValueOrNull(AttributeArgumentSyntax? argument) =>
        argument is null ? null : ValueOf(argument.Expression);
}
