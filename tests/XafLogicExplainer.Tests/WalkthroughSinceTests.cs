using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Tests;

/// <summary>
/// What changed in one process, which is the question no chat can answer.
/// </summary>
/// <remarks>
/// Ask a model what changed in the commission calculation since the last release and it can only
/// re-read today's code and describe it fluently, because it has no yesterday. This re-walks the
/// same seed over a stored snapshot and reports the difference between two computed sets.
/// <para>
/// The snapshots here are built by editing an extraction in memory rather than by extracting twice
/// from disk: the assertions are about what the comparison notices, and writing files would test
/// the file writer.
/// </para>
/// </remarks>
public class WalkthroughSinceTests
{
    /// <summary>The XPO sample, re-extracted so a test can edit it without touching the shared one.</summary>
    private static ExtractedProject Fresh() => SampleProjects.Extract(SampleProjects.XpoPath);

    private static ExtractedMethod Handler(ExtractedProject project) =>
        project.Controllers.First(c => c.ClassName == "ApproveOrderController")
            .Methods.First(m => m.Name == "ApproveAction_Execute");

    [Fact]
    public void AnUnchangedProcessReportsNothing()
    {
        var diff = WalkthroughDiff.Between(Fresh(), Fresh(), "ApproveOrder");

        Assert.True(diff.ExistedBefore);
        Assert.False(diff.AnyChange);
    }

    [Fact]
    public void AnEditedBodyIsReportedEvenWhenTheShapeOfTheProcessIsIdentical()
    {
        // The most ordinary change there is, and the one a set difference alone cannot see: somebody
        // rewrote the method and left every call in it alone. Answering "nothing changed" would be
        // the easiest lie this feature could tell.
        var previous = Fresh();
        var current = Fresh();

        Handler(current).Body = Handler(current).Body.Replace(
            "order.IsApproved = true;", "order.IsApproved = true; order.ApprovedOn = DateTime.Now;",
            StringComparison.Ordinal);

        var diff = WalkthroughDiff.Between(previous, current, "ApproveOrder");

        var change = Assert.Single(diff.Changes);

        Assert.Equal(ProcessChangeKind.Changed, change.Kind);
        Assert.Equal("ApproveOrderController.ApproveAction_Execute", change.Node.Name);

        // Nothing structural moved, which is exactly why the body had to be looked at.
        Assert.Empty(diff.StepsAdded);
        Assert.Empty(diff.StepsRemoved);
    }

    [Fact]
    public void ReformattingIsNotReportedAsAChangeOfBehaviour()
    {
        var previous = Fresh();
        var current = Fresh();

        Handler(current).Body = Handler(current).Body
            .Replace("\n", "\n    ", StringComparison.Ordinal)
            .Replace("  ", "   ", StringComparison.Ordinal);

        Assert.False(WalkthroughDiff.Between(previous, current, "ApproveOrder").AnyChange);
    }

    [Fact]
    public void ARuleThatNowGovernsTheProcessIsReported()
    {
        // One of the three the design names, and the one most likely to matter: the code did not
        // move, but what the database will now refuse did.
        var previous = Fresh();
        var current = Fresh();

        current.Entity("Order").ValidationRules.Add(new ExtractedValidationRule
        {
            RuleType = "RuleCriteria",
            Id = "Order_MustHaveLines",
            TargetCriteria = "Lines.Count > 0",
        });

        var diff = WalkthroughDiff.Between(previous, current, "ApproveOrder");
        var change = Assert.Single(diff.Changes);

        Assert.Equal(ProcessChangeKind.Added, change.Kind);
        Assert.Equal("Order_MustHaveLines", change.Node.Name);
        Assert.Contains(diff.StepsAdded, step => step.Contains("Order_MustHaveLines", StringComparison.Ordinal));
    }

    [Fact]
    public void ARuleWhoseConditionWasRewrittenIsReportedThoughItIsTheSameRule()
    {
        var previous = Fresh();
        var current = Fresh();

        current.Entity("Order").ValidationRules
            .First(rule => rule.Id == "Order_TotalNotNegative").TargetCriteria = "Total > 100";

        var change = Assert.Single(WalkthroughDiff.Between(previous, current, "ApproveOrder").Changes);

        Assert.Equal(ProcessChangeKind.Changed, change.Kind);
        Assert.Equal("Order_TotalNotNegative", change.Node.Name);
    }

    [Fact]
    public void ABranchThatIsGoneIsReportedAsRemoved()
    {
        var previous = Fresh();
        var current = Fresh();

        current.Entity("Order").AppearanceRules.Clear();

        var diff = WalkthroughDiff.Between(previous, current, "ApproveOrder");

        Assert.Contains(diff.Changes, change => change.Kind == ProcessChangeKind.Removed);
        Assert.NotEmpty(diff.StepsRemoved);
    }

    [Fact]
    public void APathTheWalkCanNoLongerFollowIsItsOwnKindOfNews()
    {
        // A process that became less knowable did change, even though every step still stands. The
        // day somebody makes a method virtual, the trace quietly stops being able to say what runs.
        var previous = SampleProjects.Extract(SampleProjects.WalkthroughPath);
        var current = SampleProjects.Extract(SampleProjects.WalkthroughPath);

        previous.Controllers.First(c => c.ClassName == "TotalsControllerBase")
            .Methods.First(m => m.Name == "Recalculate").IsOverridable = false;

        var diff = WalkthroughDiff.Between(previous, current, "RecalculateTotals");

        Assert.Contains("Recalculate", diff.BlindSpotsGained);
        Assert.Empty(diff.BlindSpotsLost);
        Assert.True(diff.AnyChange);
    }

    [Fact]
    public void AProcessThatDidNotExistBeforeSaysSoRatherThanListingItselfAsNew()
    {
        var previous = Fresh();

        previous.Controllers.Clear();

        var diff = WalkthroughDiff.Between(previous, Fresh(), "ApproveOrder");

        Assert.False(diff.ExistedBefore);

        var document = new WalkthroughGenerator("en").Generate(
            Fresh(), diff.Current, narration: null, since: diff);

        Assert.Contains("did not exist at the previous extraction", document, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSectionIsAbsentWhenNoSnapshotWasGivenAndPresentWhenOneWas()
    {
        // An absent section reads as "not asked"; an empty one has to read as "asked, and nothing".
        // A document that cannot tell those apart is worse than one that never offered.
        var project = Fresh();
        var slice = ProcessSlice.From(project, "ApproveOrder");

        var without = new WalkthroughGenerator("en").Generate(project, slice);
        var with = new WalkthroughGenerator("en").Generate(
            project, slice, narration: null, since: WalkthroughDiff.Between(Fresh(), project, "ApproveOrder"));

        Assert.DoesNotContain("What changed in this process", without, StringComparison.Ordinal);
        Assert.Contains("What changed in this process", with, StringComparison.Ordinal);
        Assert.Contains("Nothing. This process is what it was.", with, StringComparison.Ordinal);
    }
}
