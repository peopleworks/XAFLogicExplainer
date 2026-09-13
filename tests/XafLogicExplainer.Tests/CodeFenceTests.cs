using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That source copied into a code block stays inside it.
/// </summary>
/// <remarks>
/// Action bodies, seed methods, a dialog's criteria and the criteria examples in <c>AGENTS.md</c>
/// are written into fenced code blocks as they are. By CommonMark's rule a line inside that starts
/// with the same run of backticks closes the fence, and everything after it is read as Markdown:
/// headings, lists, instructions. C# that builds Markdown in a raw string literal is enough.
/// Extracted content describes the application; it must never be able to speak as the document.
/// </remarks>
public class CodeFenceTests
{
    private const string Escape = "var markdown = \"\"\"\n```\n# Injected heading\n```sql\nSELECT 1\n\"\"\";";

    [Fact]
    public void ActionCodeCannotCloseItsFence()
    {
        var project = SampleProjects.Extract(SampleProjects.XpoPath);
        project.Controllers.SelectMany(c => c.Actions).First(a => !string.IsNullOrEmpty(a.ExecuteMethodBody))
            .ExecuteMethodBody = Escape;

        AssertInjectedHeadingIsInsideAFence(Markdown(project));
    }

    [Fact]
    public void SeedCodeCannotCloseItsFence()
    {
        var project = SampleProjects.Extract(SampleProjects.DemoPath);
        project.SeedData.First(s => !string.IsNullOrEmpty(s.RawSourceCode)).RawSourceCode = Escape;

        AssertInjectedHeadingIsInsideAFence(Markdown(project));
    }

    [Fact]
    public void ADialogsCriteriaCannotCloseTheirFence()
    {
        var project = SampleProjects.Extract(SampleProjects.ReportsPath);
        project.Reports.First(r => r.ParametersObject?.CriteriaSource is { Length: > 0 })
            .ParametersObject!.CriteriaSource = Escape;

        AssertInjectedHeadingIsInsideAFence(Markdown(project));
    }

    [Fact]
    public void CriteriaExamplesInAgentsMdCannotCloseTheirFence()
    {
        var project = SampleProjects.Extract(SampleProjects.DemoPath);
        project.Entities.SelectMany(e => e.ValidationRules).First(r => !string.IsNullOrEmpty(r.Expression))
            .Expression = "ExpiresOn > IssuedOn\n```\n# Injected heading";

        AssertInjectedHeadingIsInsideAFence(new AgentContextGenerator().GenerateIndex(project, []));
    }

    [Fact]
    public void AnOrdinaryFenceKeepsThreeBackticks()
    {
        // Nearly every application has no backtick in its code at all. Its documents must come out
        // byte for byte as they did before, or every regeneration after upgrading is a diff.
        var markdown = Markdown(SampleProjects.Extract(SampleProjects.XpoPath));

        Assert.Contains("```csharp", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("````", markdown, StringComparison.Ordinal);
    }

    private static string Markdown(ExtractedProject project) =>
        string.Join("\n", new MarkdownDocumentationGenerator("en").GenerateSections(project).Select(s => s.Content));

    private static void AssertInjectedHeadingIsInsideAFence(string document)
    {
        var lines = document.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        var heading = Array.IndexOf(lines, "# Injected heading");

        Assert.True(heading >= 0, "The injected text did not reach the document at all, so this test proves nothing.");
        Assert.Contains(heading, LinesInsideFences(lines));
    }

    /// <summary>
    /// The lines that sit inside a fenced code block, by CommonMark's rule for backtick fences.
    /// </summary>
    /// <remarks>
    /// A fence opens with three or more backticks at the start of a line, indented at most three
    /// spaces. Only a line holding a run at least as long, and nothing else, closes it. A shorter run,
    /// or backticks in the middle of a line, is content.
    /// </remarks>
    private static HashSet<int> LinesInsideFences(string[] lines)
    {
        var inside = new HashSet<int>();
        var open = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var body = lines[i].TrimStart(' ');
            var indent = lines[i].Length - body.Length;
            var run = body.TakeWhile(c => c == '`').Count();

            if (open == 0)
            {
                if (indent <= 3 && run >= 3)
                    open = run;
            }
            else if (indent <= 3 && run >= open && body.TrimEnd().Length == run)
            {
                open = 0;
            }
            else
            {
                inside.Add(i);
            }
        }

        return inside;
    }
}
