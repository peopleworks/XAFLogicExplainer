namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// The fence for a code block that holds text copied out of the application.
/// </summary>
/// <remarks>
/// By CommonMark's rule, a line inside a fenced block that starts with a run of backticks at least as
/// long as the opening fence closes the block, and everything after it is read as Markdown. Source
/// copied out of an application can hold such a line: C# that builds Markdown in a raw string literal
/// is enough. So the fence is one backtick longer than the longest run in what it holds, and three
/// when there is none. That leaves every ordinary document byte for byte as it was.
/// </remarks>
internal static class MarkdownFence
{
    /// <summary>A fence no line of <paramref name="content"/> can close.</summary>
    public static string For(string? content)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in content ?? string.Empty)
        {
            run = character == '`' ? run + 1 : 0;

            if (run > longest)
                longest = run;
        }

        return new string('`', Math.Max(3, longest + 1));
    }

    /// <summary>A fence no line of any of <paramref name="contents"/> can close.</summary>
    public static string For(IEnumerable<string?> contents) => For(string.Join("\n", contents));
}
