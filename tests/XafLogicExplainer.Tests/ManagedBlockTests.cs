using XafLogicExplainer.Core.Sinks;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Writing generated content into files a human also edits.
/// </summary>
/// <remarks>
/// Two properties matter, and both were broken at first: a hand-written <c>CLAUDE.md</c> must
/// survive regeneration, and regenerating unchanged content must produce identical bytes. The
/// first implementation appended a blank line on every run, which in a repository is a spurious
/// diff every time and teaches people to stop reading them.
/// </remarks>
public class ManagedBlockTests
{
    private const string Generated = "## This XAF application\n\n@AGENTS.md";

    [Fact]
    public void CreatesTheBlockWhenTheFileIsNew()
    {
        var result = ManagedBlock.Apply(null, Generated);

        Assert.Contains(ManagedBlock.BeginMarker, result);
        Assert.Contains(ManagedBlock.EndMarker, result);
        Assert.Contains("@AGENTS.md", result);
    }

    [Fact]
    public void PreservesTextWrittenByHand()
    {
        const string handWritten = "# My notes\n\nRun the migration before deploying.\n";

        var result = ManagedBlock.Apply(handWritten, Generated);

        Assert.Contains("Run the migration before deploying.", result);
        Assert.Contains("@AGENTS.md", result);
    }

    [Fact]
    public void KeepsTextOnBothSidesOfTheBlock()
    {
        var first = ManagedBlock.Apply("# Before\n", Generated);
        var withTrailingNote = first + "\n## After\n\nA note added below the block.\n";

        var result = ManagedBlock.Apply(withTrailingNote, "## Updated\n\n@AGENTS.md");

        Assert.Contains("# Before", result);
        Assert.Contains("A note added below the block.", result);
        Assert.Contains("## Updated", result);
        Assert.DoesNotContain("This XAF application", result);
    }

    [Fact]
    public void RepeatedRunsProduceIdenticalBytes()
    {
        var first = ManagedBlock.Apply("# My notes\n\nSomething I wrote.\n", Generated);
        var second = ManagedBlock.Apply(first, Generated);
        var third = ManagedBlock.Apply(second, Generated);
        var fourth = ManagedBlock.Apply(third, Generated);

        Assert.Equal(first, second);
        Assert.Equal(second, third);
        Assert.Equal(third, fourth);
    }

    [Fact]
    public void ReplacesTheBlockRatherThanAppendingASecondOne()
    {
        var first = ManagedBlock.Apply(null, Generated);
        var second = ManagedBlock.Apply(first, "## Replaced\n\n@AGENTS.md");

        Assert.Single(Occurrences(second, ManagedBlock.BeginMarker));
        Assert.Single(Occurrences(second, ManagedBlock.EndMarker));
        Assert.Contains("## Replaced", second);
        Assert.DoesNotContain("This XAF application", second);
    }

    [Fact]
    public void KeepsFollowingTextWhenTheClosingMarkerIsMissing()
    {
        // A half-written block means someone edited the file or a previous run was interrupted.
        // Truncating at the opening marker would delete whatever came after it.
        var damaged = $"# Notes\n\n{ManagedBlock.BeginMarker}\n\nhalf a block\n\n## Important\n\nDo not lose this.\n";

        var result = ManagedBlock.Apply(damaged, Generated);

        Assert.Contains("Do not lose this.", result);
        Assert.Contains("@AGENTS.md", result);
    }

    [Fact]
    public void RecognizesFilesItAlreadyManages()
    {
        Assert.False(ManagedBlock.IsManaged("# Just my notes"));
        Assert.False(ManagedBlock.IsManaged(null));
        Assert.True(ManagedBlock.IsManaged(ManagedBlock.Apply(null, Generated)));
    }

    [Fact]
    public void EndsWithExactlyOneNewline()
    {
        var result = ManagedBlock.Apply("# Notes\n", Generated);

        Assert.EndsWith("\n", result);
        Assert.DoesNotContain("\n\n\n", result);
    }

    private static List<int> Occurrences(string haystack, string needle)
    {
        var found = new List<int>();
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (index >= 0)
        {
            found.Add(index);
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return found;
    }
}
