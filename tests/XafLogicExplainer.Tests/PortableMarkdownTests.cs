using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That the Markdown we generate is Markdown, and not Markdown plus HTML that only a browser opens.
/// </summary>
/// <remarks>
/// The seed section wrapped a method's source in a <c>&lt;details&gt;</c> fold. On GitHub that
/// collapses; anywhere else the wrapper is literal text and the fold's label — "Source code of
/// PopulateStatuses" — stops being a label and becomes a line of markup. "Anywhere else" is most
/// places these files go: a Word or PDF export, a plain Markdown viewer, and a language model, for
/// which the fold is tokens spent on something it cannot open.
/// <para>
/// Found by walking our output against the Markdig-based converter in
/// <see href="https://github.com/MBrekhof/mcpOffice">mcpOffice</see>, whose documented behaviour for
/// an HTML block is to emit it as a plain text paragraph. Every other construct we write — headings,
/// pipe tables, fenced code, lists, bold, inline code — maps to a real Word equivalent, so this one
/// call site was the whole difference between an extraction and a document someone can hand over.
/// </para>
/// </remarks>
public class PortableMarkdownTests
{
    private static readonly (string Name, ExtractedProject Project)[] Samples =
    [
        ("Xpo", SampleProjects.Xpo),
        ("EfCore", SampleProjects.EfCore),
        ("LegacyEf", SampleProjects.LegacyEf),
        ("PocoEf", SampleProjects.PocoEf),
        ("NoOrm", SampleProjects.NoOrm),
        ("DeepXpo", SampleProjects.DeepXpo),
        ("AuditedXpo", SampleProjects.AuditedXpo),
        ("Demo", SampleProjects.Demo),
    ];

    private static string Markdown(ExtractedProject project, string language) =>
        string.Join("\n", new MarkdownDocumentationGenerator(language)
            .GenerateSections(project)
            .Select(section => section.Content))
            .Replace("\r", "");

    /// <summary>
    /// Lines that begin a CommonMark HTML block: outside a fence, a line whose first character is
    /// <c>&lt;</c>. Inside a fence the same line is source code and is left alone, which is why this
    /// tracks the fence rather than matching the whole document at once.
    /// </summary>
    private static List<string> RawHtmlLines(string markdown)
    {
        var offenders = new List<string>();
        var insideFence = false;

        foreach (var line in markdown.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                continue;
            }

            if (!insideFence && line.TrimStart().StartsWith('<'))
                offenders.Add(line.Trim());
        }

        return offenders;
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void NoGeneratedPageOpensALineWithRawHtml(string language)
    {
        foreach (var (name, project) in Samples)
        {
            var offenders = RawHtmlLines(Markdown(project, language));

            Assert.True(offenders.Count == 0,
                $"{name} ({language}) emits raw HTML outside a code fence, which renders as literal "
                + $"text everywhere but a browser: {string.Join(" | ", offenders)}");
        }
    }

    [Fact]
    public void TheSeedSourceIsIntroducedByAHeadingRatherThanAFold()
    {
        // A heading survives the trip and keeps its place in the document outline; the fold did
        // neither. The fixture that carries this is the XPO sample, whose updater has a body.
        var english = Markdown(SampleProjects.Xpo, "en");

        Assert.Contains("#### Source code of ", english, StringComparison.Ordinal);
        Assert.Contains("#### Codigo fuente de ", Markdown(SampleProjects.Xpo, "es"), StringComparison.Ordinal);

        // The label is what was lost: inside <summary> it was markup, and the reader met angle
        // brackets where a title belonged.
        Assert.DoesNotContain("<summary>", english, StringComparison.Ordinal);
    }

    /// <summary>
    /// A generic type argument in running text — <c>ViewController&lt;DetailView&gt;</c> — is an
    /// inline HTML tag to CommonMark, and <c>&lt;DetailView&gt;</c> is dropped by Word and by GitHub
    /// alike. The line-opening check above never sees it, because the line opens with a bullet.
    /// Inside backticks it is text again, which is how every other type name here is written.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void NoGeneratedPageCarriesAnUnquotedAngleBracketTag(string language)
    {
        foreach (var (name, project) in Samples)
        {
            var offenders = InlineTagLines(Markdown(project, language));

            Assert.True(offenders.Count == 0,
                $"{name} ({language}) writes a type argument that renders as an HTML tag: "
                + string.Join(" | ", offenders));
        }
    }

    /// <summary>
    /// Lines, outside a fence, that still contain <c>&lt;Word&gt;</c> once inline code spans are
    /// removed.
    /// </summary>
    private static List<string> InlineTagLines(string markdown)
    {
        var offenders = new List<string>();
        var insideFence = false;

        foreach (var line in markdown.Split('\n'))
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence)
                continue;

            var prose = System.Text.RegularExpressions.Regex.Replace(line, "`[^`]*`", "");

            if (System.Text.RegularExpressions.Regex.IsMatch(prose, "<[A-Za-z][A-Za-z0-9]*>"))
                offenders.Add(line.Trim());
        }

        return offenders;
    }
}
