using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Walkthrough;
using XafLogicExplainer.Mcp;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The document a slice becomes, and the diagram inside it.
/// </summary>
/// <remarks>
/// Everything here is a rendering of a walk that was computed. That is the governing decision made
/// visible: ask a language model for a Mermaid diagram of a process and it will produce one,
/// including edges that do not exist, drawn with a confidence indistinguishable from the true ones,
/// in a format whose whole value is that a reader believes it at a glance.
/// </remarks>
public class WalkthroughDocumentTests
{
    private static string Document(ExtractedProject project, string seed, string language = "en", int depth = 3) =>
        new WalkthroughGenerator(language)
            .Generate(project, ProcessSlice.From(project, seed, depth))
            .Replace("\r", "");

    [Fact]
    public void EveryArrowInTheDiagramIsAnEdgeTheWalkFound()
    {
        // The assertion the whole feature rests on. Counted rather than sampled: an extra arrow is
        // exactly the failure that would not show up in a spot check of a diagram that looks right.
        var slice = ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder");
        var diagram = MermaidFlow.From(slice, DocumentationLabels.English());

        var arrows = diagram.Split('\n').Count(line => line.Contains("-->", StringComparison.Ordinal));

        Assert.Equal(slice.Edges.Count, arrows);

        // And every box is a node, for the same reason in the other direction.
        var boxes = diagram.Split('\n').Count(line => line.TrimStart().StartsWith('n')
                                                      && !line.Contains("-->", StringComparison.Ordinal));

        Assert.Equal(slice.Nodes.Count, boxes);
    }

    [Fact]
    public void TheDiagramDistinguishesWhatIsStoredFromWhatIsRun()
    {
        var diagram = MermaidFlow.From(
            ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder"), DocumentationLabels.English());

        Assert.Contains("([\"ApproveOrder\"])", diagram, StringComparison.Ordinal);          // action
        Assert.Contains("[[\"ApproveOrderController\"]]", diagram, StringComparison.Ordinal); // controller
        Assert.Contains("[(\"Order\")]", diagram, StringComparison.Ordinal);                  // entity
        Assert.Contains("{{\"Order_TotalNotNegative\"}}", diagram, StringComparison.Ordinal); // rule
    }

    [Fact]
    public void EveryStepCitesAPlaceTheReaderCanOpen()
    {
        var document = Document(SampleProjects.Xpo, "ApproveOrder");
        var steps = document.Split('\n')
            .Where(line => line.Length > 2 && char.IsDigit(line[0]) && line.Contains(". **", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(steps);

        // A step without a citation is an assertion the reader has to take on trust, which is the
        // one thing a traced account is for.
        Assert.All(steps, step => Assert.Matches(@"`[^`]+\.cs:\d+`$", step));
    }

    [Fact]
    public void CitationsAreRelativeAndUseForwardSlashes()
    {
        // This document gets committed and read on another machine. An absolute path is noise, and a
        // separator that changes with the extractor's operating system would put a diff in every
        // citation the day somebody else regenerates it.
        var document = Document(SampleProjects.Xpo, "ApproveOrder");

        Assert.Contains("`Controllers/ApproveOrderController.cs:11`", document, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\", document, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleProjects.XpoPath, document, StringComparison.Ordinal);
    }

    [Fact]
    public void TheOpeningDoesNotClaimCompletenessTheRestOfTheDocumentDenies()
    {
        // Found by reading the page rather than the assertions. The walk had two ways to end and the
        // prose had two sentences for them, so a walk halted by a virtual call reported that it "ran
        // out of code to follow" — three lines above a section listing the code it could not follow.
        var blocked = Document(SampleProjects.Walkthrough, "RecalculateTotals");

        Assert.Contains("ran out of calls it could resolve", blocked, StringComparison.Ordinal);
        Assert.DoesNotContain("ran out of code to follow", blocked, StringComparison.Ordinal);

        var complete = Document(SampleProjects.Xpo, "ApproveOrder");

        Assert.Contains("ran out of code to follow", complete, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatCouldNotBeFollowedIsAlwaysItsOwnSection()
    {
        // Present even when empty, so its absence never has to be interpreted: a reader who finds
        // "Nothing" knows the path is whole, where a missing heading could mean either.
        var complete = Document(SampleProjects.Xpo, "ApproveOrder");

        Assert.Contains("## What this walk could not follow", complete, StringComparison.Ordinal);
        Assert.Contains("Every call in this slice resolved", complete, StringComparison.Ordinal);

        var blocked = Document(SampleProjects.Walkthrough, "RecalculateTotals");

        Assert.Contains("`Recalculate`", blocked, StringComparison.Ordinal);
        Assert.Contains("`InvoiceTotalsController.Recalculate`", blocked, StringComparison.Ordinal);
        Assert.Contains("`CreditNoteTotalsController.Recalculate`", blocked, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentStatesItsOwnBounds()
    {
        var bounded = Document(SampleProjects.Xpo, "ApproveOrder", depth: 1);

        Assert.Contains("Depth limit: 1, reached", bounded, StringComparison.Ordinal);
        Assert.Contains("missing by design", bounded, StringComparison.Ordinal);

        var whole = Document(SampleProjects.Xpo, "ApproveOrder", depth: 6);

        Assert.Contains("Depth limit: 6, not reached", whole, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWholeDocumentIsTranslated()
    {
        // The rule the rest of the generators follow. A page half in English is worse than either.
        var spanish = Document(SampleProjects.Xpo, "ApproveOrder", "es");

        Assert.Contains("# Recorrido — ApproveOrder", spanish, StringComparison.Ordinal);
        Assert.Contains("Parte de **ApproveOrder** — accion", spanish, StringComparison.Ordinal);
        Assert.Contains("## Lo que participa", spanish, StringComparison.Ordinal);
        Assert.Contains("## Paso a paso", spanish, StringComparison.Ordinal);
        Assert.Contains("declara", spanish, StringComparison.Ordinal);

        foreach (var english in (string[])["Step by step", "carries the rule", "What this walk", "Flow"])
            Assert.DoesNotContain(english, spanish, StringComparison.Ordinal);
    }

    [Fact]
    public void ASeedThatMatchedNothingProducesTheReasonRatherThanAnEmptyDocument()
    {
        var document = Document(SampleProjects.Xpo, "Approve");

        Assert.Contains("ApproveOrder", document, StringComparison.Ordinal);
        Assert.DoesNotContain("## Flow", document, StringComparison.Ordinal);
        Assert.DoesNotContain("mermaid", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAgentGetsTheSameAccountAsThePage()
    {
        // The reason this tool exists: every other one returns an atom, so an agent asked how
        // something works has to guess which atoms to fetch and then guess whether it has them all.
        var tools = new XafWalkthroughTools(new XafProjectContext(
            [new XafProjectSource { Name = "SampleApp", Path = SampleProjects.XpoPath, Language = "en" }]));

        var answer = await tools.WalkthroughAsync(
            "ApproveOrder", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("flowchart TD", answer, StringComparison.Ordinal);
        Assert.Contains("Order_TotalNotNegative", answer, StringComparison.Ordinal);
        Assert.Contains("What this walk could not follow", answer, StringComparison.Ordinal);
    }
}
