using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Wiki;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That the pictures of the corpus are arithmetic rather than decoration.
/// </summary>
/// <remarks>
/// A diagram is believed faster than a sentence and checked less, so the placement rules have to
/// hold exactly: the middle of the map means <em>everybody models this</em>, and if that is only
/// approximately true the picture is lying at a glance. Layout is computed apart from the markup so
/// it can be asserted here without parsing HTML.
/// </remarks>
public class CorpusGraphTests
{
    // ---------------------------------------------------------------- which two are alike

    /// <summary>
    /// The overlap of two applications is the number of class names both model, both ways round.
    /// </summary>
    [Fact]
    public void OverlapCountsSharedNamesSymmetrically()
    {
        var grid = CorpusGraph.Overlap(Corpus(
            App("A", "Cliente", "Factura", "Solo"),
            App("B", "Cliente", "Factura")));

        Assert.Equal(2, Cell(grid, 0, 1).Shared);
        Assert.Equal(2, Cell(grid, 1, 0).Shared);
        Assert.Equal(2, grid.Highest);
    }

    /// <summary>
    /// The diagonal counts what an application has of its own, and is never mistaken for an overlap.
    /// </summary>
    [Fact]
    public void DiagonalIsTheApplicationItself()
    {
        var grid = CorpusGraph.Overlap(Corpus(
            App("A", "Cliente", "Factura", "Solo"),
            App("B", "Cliente")));

        var diagonal = Cell(grid, 0, 0);
        Assert.True(diagonal.IsSelf);
        Assert.Equal(3, diagonal.Shared);

        // Its 3 must not become the top of the colour scale, or one application having many classes
        // would wash out every real overlap on the grid.
        Assert.Equal(1, grid.Highest);
    }

    /// <summary>
    /// Applications with nothing in common produce a grid that says so.
    /// </summary>
    [Fact]
    public void GridWithNoOverlapIsEmpty()
    {
        var grid = CorpusGraph.Overlap(Corpus(App("A", "Uno"), App("B", "Dos")));

        Assert.True(grid.IsEmpty);
    }

    // ---------------------------------------------------------------- the map

    /// <summary>
    /// A class every application models has no direction to sit in, so it sits in the middle.
    /// </summary>
    /// <remarks>
    /// This is the one claim the picture makes without words. If it were only roughly true, a reader
    /// would take a class shared by two of five applications for common ground.
    /// </remarks>
    [Fact]
    public void ClassEveryApplicationModelsFallsToTheCentre()
    {
        var map = CorpusGraph.Map(Corpus(
            App("A", "Cliente"), App("B", "Cliente"), App("C", "Cliente"), App("D", "Cliente")));

        var shared = Assert.Single(map.Classes);

        Assert.Equal(map.Width / 2, shared.X, 0.5);
        Assert.Equal(map.Height / 2, shared.Y, 0.5);
    }

    /// <summary>
    /// A class two applications share sits between those two, not in the middle.
    /// </summary>
    [Fact]
    public void ClassTwoApplicationsShareSitsBetweenThem()
    {
        var map = CorpusGraph.Map(Corpus(
            App("A", "Cliente"), App("B", "Cliente"), App("C", "Otro"), App("D", "Otro2")));

        var shared = Assert.Single(map.Classes);
        var a = map.Applications.Single(x => x.Name == "A");
        var b = map.Applications.Single(x => x.Name == "B");

        var toA = Distance(shared.X, shared.Y, a.X, a.Y);
        var toB = Distance(shared.X, shared.Y, b.X, b.Y);
        var toC = Distance(shared.X, shared.Y,
            map.Applications.Single(x => x.Name == "C").X,
            map.Applications.Single(x => x.Name == "C").Y);

        Assert.True(toA < toC, "The class should sit nearer the applications that model it.");
        Assert.Equal(toA, toB, 0.5);
        Assert.True(Distance(shared.X, shared.Y, map.Width / 2, map.Height / 2) > 1,
            "A class only two of four applications model is not common ground.");
    }

    /// <summary>
    /// Classes shared by exactly the same applications are drawn apart rather than on top of
    /// each other.
    /// </summary>
    [Fact]
    public void ClassesWithTheSameOwnersDoNotOverlap()
    {
        var map = CorpusGraph.Map(Corpus(
            App("A", "Cliente", "Factura", "Cobro"),
            App("B", "Cliente", "Factura", "Cobro")));

        var points = map.Classes.Select(c => (c.X, c.Y)).ToList();

        Assert.Equal(3, points.Distinct().Count());
        Assert.Equal(3, map.Classes.Select(c => (c.LabelX, c.LabelY)).Distinct().Count());
    }

    /// <summary>
    /// The same corpus draws the same picture every run.
    /// </summary>
    /// <remarks>
    /// A diagram that moves between two runs of the same tool cannot be used to compare two runs,
    /// which is most of what a diagram in a wiki is for.
    /// </remarks>
    [Fact]
    public void LayoutIsTheSameEveryRun()
    {
        var corpus = Corpus(
            App("A", "Cliente", "Factura"),
            App("B", "Cliente", "Producto"),
            App("C", "Factura", "Producto"));

        var first = CorpusGraph.Map(corpus);
        var second = CorpusGraph.Map(corpus);

        Assert.Equal(
            first.Classes.Select(c => $"{c.ClassName}:{c.X:F4},{c.Y:F4}"),
            second.Classes.Select(c => $"{c.ClassName}:{c.X:F4},{c.Y:F4}"));
    }

    /// <summary>
    /// One application, or nothing shared, means there is no map to draw.
    /// </summary>
    [Fact]
    public void NothingSharedMeansNoMap()
    {
        Assert.True(CorpusGraph.Map(Corpus(App("Solo", "Cliente"))).IsEmpty);
        Assert.True(CorpusGraph.Map(Corpus(App("A", "Uno"), App("B", "Dos"))).IsEmpty);
    }

    /// <summary>
    /// Every application that models a class is joined to it, and nothing else is.
    /// </summary>
    [Fact]
    public void LinksJoinExactlyTheApplicationsThatModelTheClass()
    {
        var map = CorpusGraph.Map(Corpus(
            App("A", "Cliente"), App("B", "Cliente"), App("C", "Otro"), App("D", "Otro")));

        var cliente = map.Links.Where(l => l.ClassName == "Cliente").Select(l => l.Slug).Order().ToList();

        Assert.Equal(["a", "b"], cliente);
    }

    // ---------------------------------------------------------------- the releases

    /// <summary>
    /// Versions are ordered as numbers, not as text.
    /// </summary>
    /// <remarks>
    /// Sorted as text, <c>26.1</c> comes before <c>9.2</c>, and the strip would show a nine-year-old
    /// application as the newest thing in the estate.
    /// </remarks>
    [Fact]
    public void VersionsAreOrderedNumerically()
    {
        var spread = CorpusGraph.Versions(Corpus(
            AppOn("Old", "9.2"), AppOn("New", "26.1"), AppOn("Middle", "17.1")));

        Assert.Equal(["9.2", "17.1", "26.1"], spread.Stops);
        Assert.Equal("Old", spread.Applications[0].Name);
        Assert.True(spread.IsSplit);
    }

    /// <summary>
    /// An application whose declared version could not be read is counted, not quietly dropped.
    /// </summary>
    [Fact]
    public void UndeclaredVersionsAreCounted()
    {
        var spread = CorpusGraph.Versions(Corpus(AppOn("Known", "25.2"), App("Unknown", "Cliente")));

        Assert.Equal(1, spread.Undeclared);
        Assert.Single(spread.Applications);
        Assert.False(spread.IsSplit);
    }

    // ---------------------------------------------------------------- on the page

    /// <summary>
    /// The map, the grid and the strip all reach the generated page.
    /// </summary>
    [Fact]
    public void PicturesReachThePage()
    {
        var html = new WikiGenerator("0.0.0-test").Generate(Corpus(
            AppOn("Legal", "23.2", "Cliente", "Factura"),
            AppOn("Presupuesto", "26.1", "Cliente", "Producto")));

        Assert.Contains("id=\"cmap\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"anode\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"cnode", html, StringComparison.Ordinal);
        Assert.Contains("class=\"heat\"", html, StringComparison.Ordinal);
        Assert.Contains("data-pair=\"legal presupuesto\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"spread\"", html, StringComparison.Ordinal);
        Assert.Contains(">23.2</div>", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A corpus with nothing to draw shows no empty diagram, and no heading over one.
    /// </summary>
    [Fact]
    public void NoPicturesWhenThereIsNothingToDraw()
    {
        var html = new WikiGenerator("0.0.0-test").Generate(Corpus(App("Solo", "Cliente")));

        Assert.DoesNotContain("id=\"cmap\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Where your applications meet", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"#map\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A count of one entity does not reach the page as "1 entitys".
    /// </summary>
    /// <remarks>
    /// Small, and the kind of thing a reader takes as evidence that nobody looked at the output.
    /// </remarks>
    [Fact]
    public void CountsAreSpelledCorrectly()
    {
        var html = new WikiGenerator("0.0.0-test").Generate(Corpus(
            App("A", "Cliente"), App("B", "Cliente")));

        Assert.DoesNotContain("entitys", html, StringComparison.Ordinal);
        Assert.Contains("entity", html, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- helpers

    private static OverlapCell Cell(OverlapGrid grid, int row, int column) =>
        grid.Cells.Single(c => c.Row == row && c.Column == column);

    private static double Distance(double x1, double y1, double x2, double y2) =>
        Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));

    private static WikiCorpus Corpus(params WikiApplication[] applications) =>
        CorpusAnalyzer.Analyze(applications);

    private static WikiApplication App(string name, params string[] classNames) =>
        AppOn(name, null, classNames);

    private static WikiApplication AppOn(string name, string? devExpressVersion, params string[] classNames) =>
        new()
        {
            Name = name,
            Slug = name.ToLowerInvariant(),
            Project = new ExtractedProject
            {
                ProjectName = name,
                ProjectPath = Path.Combine(Path.GetTempPath(), name),
                DeclaredDevExpressVersion = devExpressVersion,
                Entities = [.. classNames.Select(c => new ExtractedEntity
                {
                    ClassName = c,
                    Namespace = "Fixture.Module.BusinessObjects",
                    BaseType = "BaseObject",
                    FilePath = Path.Combine(Path.GetTempPath(), name, $"{c}.cs"),
                    Line = 10,
                    Properties = [new ExtractedProperty { Name = "Nombre", TypeName = "string" }],
                })],
            },
        };
}
