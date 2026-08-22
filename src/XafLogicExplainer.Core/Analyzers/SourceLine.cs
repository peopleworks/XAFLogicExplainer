using Microsoft.CodeAnalysis;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Where a declaration is, as a line a person or an agent can open.
/// </summary>
/// <remarks>
/// Taken from the <b>identifier token</b> rather than from the declaration's span. A span starts at
/// the first attribute, so a class carrying <c>[DefaultClassOptions]</c> would be cited one or more
/// lines above the word <c>class</c> — correct about the syntax and wrong about what the reader is
/// looking for. The identifier is the line the name is on, which is the line anybody means.
/// </remarks>
internal static class SourceLine
{
    /// <summary>The one-based line the token sits on, or zero when there is no source to point at.</summary>
    /// <remarks>
    /// Zero rather than one for the unknown case: a line number is a claim about a file, and one is
    /// a plausible-looking claim. Zero is not a line, so nothing renders it as though it were.
    /// </remarks>
    internal static int Of(SyntaxToken token)
    {
        var location = token.GetLocation();

        return location.Kind == LocationKind.SourceFile
            ? location.GetLineSpan().StartLinePosition.Line + 1
            : 0;
    }
}
