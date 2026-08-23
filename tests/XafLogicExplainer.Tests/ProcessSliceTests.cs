using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The walk that decides what belongs to one business process.
/// </summary>
/// <remarks>
/// The scope is computed here rather than asked for, which is the whole governing decision of the
/// walkthrough: a model asked what belongs in "the approval process" answers authoritatively, in a
/// form nobody can check, and is wrong in places that look exactly like the places it is right. A
/// bounded walk answers something reviewable, that diffs between two extractions, and whose bad
/// sentences can be fixed without touching its structure.
/// </remarks>
public class ProcessSliceTests
{
    private static IEnumerable<string> Names(ProcessSlice slice, SliceNodeKind kind) =>
        slice.Nodes.Where(node => node.Kind == kind).Select(node => node.Name);

    private static bool HasEdge(ProcessSlice slice, string from, string to, SliceEdgeKind kind) =>
        slice.Edges.Any(edge => edge.From == from && edge.To == to && edge.Kind == kind);

    [Fact]
    public void AnActionReachesTheCodeItRunsAndTheRulesThatGovernWhatItWrites()
    {
        var slice = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder");

        Assert.True(slice.Found);
        Assert.Equal(SliceNodeKind.Action, slice.Root!.Kind);

        Assert.Equal(["ApproveOrderController"], Names(slice, SliceNodeKind.Controller));
        Assert.Equal(["ApproveOrderController.ApproveAction_Execute"], Names(slice, SliceNodeKind.Method));

        // Order because the handler casts to it; Customer because the handler reads `order.Customer`
        // and the model declares that relationship.
        Assert.Equal(["Customer", "Order"], Names(slice, SliceNodeKind.Entity).Order());

        // The reason an entity is in the slice at all: what the process writes is checked on save.
        Assert.Contains("Order_TotalNotNegative", Names(slice, SliceNodeKind.ValidationRule));
        Assert.Contains("OrderLockedWhenApproved", Names(slice, SliceNodeKind.AppearanceRule));
    }

    [Fact]
    public void TheControllerDeclaresTheActionRatherThanTheOtherWayRound()
    {
        // The walk reaches the controller by climbing up from the action, but the fact is the other
        // way round — and phase 2 draws arrows straight off this set, so an edge written backwards
        // is an arrow in a diagram that the code does not support.
        var slice = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder");

        Assert.True(HasEdge(slice,
            "controller:ApproveOrderController",
            "action:ApproveOrderController.ApproveOrder",
            SliceEdgeKind.Declares));

        Assert.DoesNotContain(slice.Edges, edge =>
            edge.From.StartsWith("action:", StringComparison.Ordinal) && edge.Kind == SliceEdgeKind.Declares);
    }

    [Fact]
    public void AnActionHandsItsBodyToItsHandlerRatherThanWalkingItTwice()
    {
        // An action's extracted body *is* its handler's body. Walking both gave the action a copy of
        // every edge the handler had — two nodes reporting one call, and a diagram forking where the
        // code does not.
        var slice = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder");

        Assert.DoesNotContain(slice.Edges, edge =>
            edge.From == "action:ApproveOrderController.ApproveOrder" && edge.Kind == SliceEdgeKind.Touches);

        Assert.True(HasEdge(slice,
            "method:ApproveOrderController.ApproveAction_Execute", "entity:Order", SliceEdgeKind.Touches));
    }

    [Fact]
    public void TheBoundIsReportedRatherThanLookingLikeTheEndOfTheProcess()
    {
        // A walk that ran out of things to reach is a whole process. A walk that hit its limit is a
        // view of one. Rendering them identically is how a document claims completeness it lacks.
        var bounded = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder", depth: 1);
        var whole = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder", depth: 6);

        Assert.True(bounded.DepthReached);
        Assert.Empty(Names(bounded, SliceNodeKind.ValidationRule));

        Assert.False(whole.DepthReached);
        Assert.NotEmpty(Names(whole, SliceNodeKind.ValidationRule));

        Assert.All(bounded.Nodes, node => Assert.True(node.Distance <= 1));
    }

    [Fact]
    public void AVirtualCallIsFollowedToWhatIsWrittenAndReportedAsWhatItMayRun()
    {
        // The case syntax cannot decide. The handler calls `Recalculate()`, one declaration sits
        // beside the call, and two overrides replace it depending on which controller XAF activated.
        var slice = ProcessSlice.From(SampleProjects.Walkthrough, "RecalculateTotals");

        // Followed: the declaration written beside the call is a real edge, so the graph is whole.
        Assert.True(HasEdge(slice,
            "method:TotalsControllerBase.RecalculateAction_Execute",
            "method:TotalsControllerBase.Recalculate",
            SliceEdgeKind.Calls));

        var reported = Assert.Single(slice.Unresolved);

        Assert.Equal("Recalculate", reported.CallName);
        Assert.Contains("InvoiceTotalsController.Recalculate", reported.Candidates);
        Assert.Contains("CreditNoteTotalsController.Recalculate", reported.Candidates);
        Assert.Contains("run-time type", reported.Reason, StringComparison.Ordinal);

        // And the consequence, which is the reason it has to be said: the bodies that do the
        // arithmetic are not in the slice, so neither are the entities they write. A walk that
        // stopped here in silence would read as a complete account of a process it never entered.
        Assert.Empty(Names(slice, SliceNodeKind.Entity));
    }

    [Fact]
    public void AnOverrideIsNotItselfReportedAsAmbiguous()
    {
        // `InvoiceTotalsController.Recalculate` is an override, not a virtual declaration: nothing
        // replaces it, so nothing about it is undecided.
        var slice = ProcessSlice.From(SampleProjects.Walkthrough, "InvoiceTotalsController");

        Assert.Empty(slice.Unresolved);
        Assert.Contains("Invoice", Names(slice, SliceNodeKind.Entity));
    }

    [Fact]
    public void AControllerAskedAboutDirectlyContributesWhatItDeclares()
    {
        // Reached from one of its own actions, a controller contributes what that action runs —
        // a walkthrough of "what happens when I press Approve" does not want the other buttons.
        // Asked about directly it is the subject, and a helper nothing calls is still part of it.
        var slice = ProcessSlice.From(SampleProjects.Walkthrough, "InvoiceTotalsController");

        Assert.Contains("InvoiceTotalsController.LineTotal", Names(slice, SliceNodeKind.Method));
        Assert.Contains("InvoiceTotalsController.Recalculate", Names(slice, SliceNodeKind.Method));
    }

    [Fact]
    public void ASeedThatMatchesNothingSaysSoAndOffersWhatItDidFind()
    {
        var slice = ProcessSlice.From(SampleProjects.Xpo, "Approve");

        Assert.False(slice.Found);
        Assert.Null(slice.Root);
        Assert.Empty(slice.Nodes);

        // Not a bare failure: `Approve` is most of a real name, and saying so is the difference
        // between a dead end and one more keystroke.
        Assert.Contains("ApproveOrder", slice.Problem!, StringComparison.Ordinal);
    }
}
