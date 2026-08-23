using System.Text.RegularExpressions;
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
/// <para>
/// The second offender was found the same way, by him, one release later: a generic base type was
/// printed bare, and <c>&lt;DetailView&gt;</c> is an <em>inline</em> tag rather than a block. The
/// guard below missed it because it was written to the shape of the first one.
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
        ("Walkthrough", SampleProjects.Walkthrough),
        ("Appearance", SampleProjects.Appearance),
    ];

    private static string Markdown(ExtractedProject project, string language) =>
        string.Join("\n", new MarkdownDocumentationGenerator(language)
            .GenerateSections(project)
            .Select(section => section.Content))
            .Replace("\r", "");

    /// <summary>
    /// Every tag CommonMark would treat as HTML, wherever on the line it sits.
    /// </summary>
    /// <remarks>
    /// The first version checked only whether a line <em>opened</em> with <c>&lt;</c>, which was the
    /// shape of the <c>&lt;details&gt;</c> block it was written for. A generic type in the middle of
    /// a sentence walked straight past it: <c>ViewController&lt;DetailView&gt;</c> is an inline HTML
    /// tag to every CommonMark parser, so the reader was shown <c>ViewController</c> and nothing
    /// else — on github.com, where the sanitizer strips the unknown tag, as much as in an export.
    /// <para>
    /// Two things are excluded rather than matched. A fenced block is source code, where
    /// <c>CreateObject&lt;Customer&gt;()</c> is exactly right. An inline code span is the remedy
    /// itself, so it has to be allowed or the guard would reject its own fix.
    /// </para>
    /// </remarks>
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

            if (insideFence)
                continue;

            var bare = Regex.Replace(line, "`[^`]*`", "");

            if (Regex.IsMatch(bare, "</?[A-Za-z][A-Za-z0-9-]*[^<>]*/?>"))
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
                $"{name} ({language}) emits a tag CommonMark reads as HTML. A block renders as "
                + "literal text everywhere but a browser; an inline tag is dropped and takes the "
                + $"type name with it: {string.Join(" | ", offenders)}");
        }
    }

    [Fact]
    public void AGenericTypeSurvivesBecauseItIsWrittenAsCode()
    {
        // Found by @MBrekhof pointing a real Word converter at the output. `ViewController<DetailView>`
        // was printed bare, and `<DetailView>` is an inline HTML tag to every CommonMark parser: an
        // export drops it and github.com's sanitizer strips it, so the reader is told the base class
        // is `ViewController`. That is a different answer rather than a missing one.
        var english = Markdown(SampleProjects.Xpo, "en");

        Assert.Contains("`ViewController<DetailView>`", english, StringComparison.Ordinal);
        Assert.DoesNotContain("** ViewController<DetailView>", english, StringComparison.Ordinal);
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
}
