using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Analyzers;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Reading the value an attribute argument stands for, without a compilation.
/// </summary>
/// <remarks>
/// Regression cover for a bug that reached generated documentation: attribute values were read as
/// <c>expression.ToString().Trim('"')</c>, so <c>[XafDefaultProperty(nameof(Number))]</c> yielded
/// the literal text "nameof(Number)" as a property name.
/// </remarks>
public class SyntaxLiteralTests
{
    [Theory]
    [InlineData("\"Plain text\"", "Plain text")]
    [InlineData("\"\"", "")]
    [InlineData("\"Text with \\\"quotes\\\" inside\"", "Text with \"quotes\" inside")]
    public void ReadsStringLiterals(string expression, string expected) =>
        Assert.Equal(expected, SyntaxLiteral.ValueOf(Parse(expression)));

    [Theory]
    [InlineData("nameof(Number)", "Number")]
    [InlineData("nameof(Order.Number)", "Number")]
    [InlineData("nameof(SampleApp.Module.BusinessObjects.Order.Total)", "Total")]
    public void ResolvesNameOf(string expression, string expected) =>
        Assert.Equal(expected, SyntaxLiteral.ValueOf(Parse(expression)));

    [Fact]
    public void JoinsConcatenatedStrings() =>
        Assert.Equal(
            "An order total cannot be negative.",
            SyntaxLiteral.ValueOf(Parse("\"An order total \" + \"cannot be negative.\"")));

    [Fact]
    public void JoinsConcatenatedStringAndNameOf() =>
        Assert.Equal("Field: Number", SyntaxLiteral.ValueOf(Parse("\"Field: \" + nameof(Number)")));

    [Theory]
    [InlineData("DefaultContexts.Save", "DefaultContexts.Save")]
    [InlineData("42", "42")]
    [InlineData("true", "true")]
    [InlineData("typeof(Customer)", "typeof(Customer)")]
    public void FallsBackToSourceTextForNonLiterals(string expression, string expected) =>
        Assert.Equal(expected, SyntaxLiteral.ValueOf(Parse(expression)));

    [Fact]
    public void TreatsMethodCallsNamedLikeNameOfAsOrdinaryExpressions()
    {
        // Only the nameof operator collapses to an identifier. A call that merely takes one
        // argument must not be mistaken for it.
        Assert.Equal("GetName(Number)", SyntaxLiteral.ValueOf(Parse("GetName(Number)")));
    }

    [Fact]
    public void ReturnsNullForAbsentExpressions()
    {
        Assert.Null(SyntaxLiteral.ValueOrNull((ExpressionSyntax?)null));
        Assert.Null(SyntaxLiteral.ValueOrNull((AttributeArgumentSyntax?)null));
    }

    private static ExpressionSyntax Parse(string expression) =>
        SyntaxFactory.ParseExpression(expression);
}
