using System.Text.RegularExpressions;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Wiki;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That the page a reader opens says what the analysis found, and admits what it did not.
/// </summary>
/// <remarks>
/// These assertions are against the generated HTML rather than against the corpus object, because
/// the two defects worth catching here are invisible in the analysis and invisible in a diff: a
/// citation carrying somebody's machine path into a file they send to a colleague, and a heading
/// standing over nothing because the honest empty-state sentence was never written.
/// </remarks>
public class WikiRenderingTests
{
    // ------------------------------------------------------- it has to survive being sent

    /// <summary>
    /// One file. No stylesheet, no script, no image fetched from anywhere.
    /// </summary>
    /// <remarks>
    /// The wiki is opened by double-click from a folder, a network share, or an email attachment,
    /// on a machine that may have no internet. One external reference and half of it is blank.
    /// </remarks>
    [Fact]
    public void PageAsksTheNetworkForNothing()
    {
        var html = Render(Corpus(FromFixture("Sample", SampleProjects.Xpo), FromFixture("Demo", SampleProjects.Demo)));

        Assert.DoesNotContain("<script src", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"http", html, StringComparison.OrdinalIgnoreCase);

        // The only address on the page is the one telling a reader where the tool came from.
        var links = Regex.Matches(html, "href=\"(https?://[^\"]+)\"").Select(m => m.Groups[1].Value).Distinct();
        Assert.All(links, link => Assert.StartsWith("https://github.com/peopleworks/XAFLogicExplainer", link, StringComparison.Ordinal));
    }

    /// <summary>
    /// A citation never carries the path it was read from on this machine.
    /// </summary>
    /// <remarks>
    /// The same defect was found in the reports work by reading the generated document rather than
    /// the diff, which is why it is asserted here rather than trusted.
    /// </remarks>
    [Fact]
    public void CitationsAreRelativeToTheProject()
    {
        var html = Render(Corpus(FromFixture("Sample", SampleProjects.Xpo), FromFixture("Demo", SampleProjects.Demo)));

        var citations = Regex.Matches(html, "class=\"cite\">([^<]+)<").Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(citations);
        Assert.All(citations, citation =>
        {
            Assert.DoesNotContain(":\\", citation, StringComparison.Ordinal);
            Assert.DoesNotContain(":/", citation, StringComparison.Ordinal);
        });
    }

    // ------------------------------------------------------- the empty states

    /// <summary>
    /// One application is a legitimate result, and the page says so instead of showing a heading
    /// above nothing.
    /// </summary>
    [Fact]
    public void CorpusOfOneSaysWhyItHasNothingToCompare()
    {
        var html = Render(Corpus(App("Solo", Entity("Cliente", Prop("Nombre", "string")))));

        Assert.Contains("A corpus of one has nothing to compare itself against", html, StringComparison.Ordinal);
        Assert.Contains("xaflogic projects add", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Applications with nothing in common get told that, in a sentence that reads as a finding.
    /// </summary>
    [Fact]
    public void NothingInCommonIsStatedAsAFinding()
    {
        var html = Render(Corpus(
            App("A", Entity("Expediente", Prop("Numero", "string"))),
            App("B", Entity("Partida", Prop("Monto", "decimal")))));

        Assert.Contains("share no class name", html, StringComparison.Ordinal);
        Assert.Contains("That is itself a finding", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- the comparison

    /// <summary>
    /// The comparison table has one column per application, and marks who has what.
    /// </summary>
    [Fact]
    public void ComparisonHasAColumnPerApplication()
    {
        var html = Render(Corpus(
            App("Legal", Entity("Cliente", Prop("Nombre", "string"), Prop("Rnc", "string"))),
            App("Presupuesto", Entity("Cliente", Prop("Nombre", "string")))));

        var matrix = Between(html, "<table class=\"matrix\">", "</table>");

        Assert.Contains("<th class=\"app\">Legal</th>", matrix, StringComparison.Ordinal);
        Assert.Contains("<th class=\"app\">Presupuesto</th>", matrix, StringComparison.Ordinal);

        // Rnc is in one of the two, so its row is marked and one cell is empty.
        var rnc = Between(matrix, ">Rnc</td>", "</tr>");
        Assert.Contains("mark has", rnc, StringComparison.Ordinal);
        Assert.Contains("mark hasnt", rnc, StringComparison.Ordinal);
    }

    /// <summary>
    /// A class two applications model differently says which one to open first.
    /// </summary>
    [Fact]
    public void RichestApplicationIsNamedOnTheCard()
    {
        var html = Render(Corpus(
            App("Thin", Entity("Cliente", Prop("Nombre", "string"))),
            App("Rich", Entity("Cliente", Prop("Nombre", "string"), Prop("Rnc", "string")))));

        Assert.Contains("<strong>Rich</strong> models it in the", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A scalar disagreement reaches the page under its own heading.
    /// </summary>
    [Fact]
    public void ScalarDisagreementGetsItsOwnHeading()
    {
        var html = Render(Corpus(
            App("A", Entity("Factura", Prop("Total", "decimal"))),
            App("B", Entity("Cobro", Prop("Total", "double")))));

        Assert.Contains("The same name, two shapes", html, StringComparison.Ordinal);

        var section = Between(html, "The same name, two shapes", "</section>");
        Assert.Contains("pill--own\">decimal</span>", section, StringComparison.Ordinal);
        Assert.Contains("pill--own\">double</span>", section, StringComparison.Ordinal);
    }

    /// <summary>
    /// A name holding a different thing in each entity is vocabulary, not a disagreement.
    /// </summary>
    [Fact]
    public void PerEntityCollectionsAreNotShownAsADisagreement()
    {
        var html = Render(Corpus(
            App("A", Entity("Factura", Prop("Details", "XPCollection<FacturaDetalle>"))),
            App("B", Entity("Cobro", Prop("Details", "XPCollection<CobroDetalle>")))));

        Assert.DoesNotContain("The same name, two shapes", html, StringComparison.Ordinal);
        Assert.Contains("Names you keep", html, StringComparison.Ordinal);
        Assert.Contains("types, one per entity", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- what it cannot tell you

    /// <summary>
    /// The page always states that classes are matched by name and may not be the same idea.
    /// </summary>
    [Fact]
    public void NameMatchingIsAlwaysDeclaredAsALimit()
    {
        var html = Render(Corpus(
            App("A", Entity("Cliente", Prop("Nombre", "string"))),
            App("B", Entity("Cliente", Prop("Nombre", "string")))));

        Assert.Contains("Two classes sharing a name may share nothing else", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page says a base class in a library nobody opened is absent rather than absent-because-
    /// framework, so its silence is never read as a claim.
    /// </summary>
    [Fact]
    public void MissingSharedLibraryIsDeclaredAsALimit()
    {
        var html = Render(Corpus(App("A", Entity("Cliente", Prop("Nombre", "string")))));

        Assert.Contains("A base class in a library you did not add is missing", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// An application that registers the reports module carries the lower-bound sentence into the
    /// wiki, exactly as it does into its own explainer.
    /// </summary>
    [Fact]
    public void ReportsLowerBoundReachesTheWiki()
    {
        var html = Render(Corpus(FromFixture("Invoicing", SampleProjects.Reports)));

        Assert.Contains("A lower bound.", html, StringComparison.Ordinal);
        Assert.Contains("The number is unknown, not zero", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- navigating a corpus

    /// <summary>
    /// Every application gets a filter, and every finding says which applications it touches.
    /// </summary>
    /// <remarks>
    /// A finding with no <c>data-app</c> disappears the moment somebody filters by an application,
    /// which reads as "this project has nothing in common with the others".
    /// </remarks>
    [Fact]
    public void EveryFindingKnowsWhichApplicationsItTouches()
    {
        var html = Render(Corpus(
            App("Legal", Entity("Cliente", Prop("Nombre", "string"))),
            App("Presupuesto", Entity("Cliente", Prop("Nombre", "string"), Prop("Rnc", "string")))));

        Assert.Contains("data-slug=\"legal\"", html, StringComparison.Ordinal);
        Assert.Contains("data-slug=\"presupuesto\"", html, StringComparison.Ordinal);

        var shared = Between(html, "<section id=\"shared\">", "</section>");
        var cards = Regex.Matches(shared, "<article class=\"card\"([^>]*)>");

        Assert.NotEmpty(cards);
        Assert.All(cards, card => Assert.Contains("data-app=\"", card.Groups[1].Value, StringComparison.Ordinal));
    }

    /// <summary>
    /// Text read from source is encoded, so a caption with an ampersand cannot break the page.
    /// </summary>
    [Fact]
    public void TextFromSourceIsEncoded()
    {
        var app = App("A");
        app.Project.Controllers =
        [
            new ExtractedController
            {
                ClassName = "C",
                BaseControllerType = "ViewController",
                Actions =
                [
                    new ExtractedAction { ActionId = "Ship", Caption = "Ship & <invoice>", ActionType = "SimpleAction" },
                ],
            },
        ];

        var html = Render(Corpus(app));

        Assert.Contains("Ship &amp; &lt;invoice&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ship & <invoice>", html, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ helpers

    private static string Render(WikiCorpus corpus) => new WikiGenerator("0.0.0-test").Generate(corpus);

    private static WikiCorpus Corpus(params WikiApplication[] applications) =>
        CorpusAnalyzer.Analyze(applications);

    private static WikiApplication FromFixture(string name, ExtractedProject project) =>
        new() { Name = name, Slug = name.ToLowerInvariant(), Project = project };

    private static WikiApplication App(string name, params ExtractedEntity[] entities) =>
        new()
        {
            Name = name,
            Slug = name.ToLowerInvariant(),
            Project = new ExtractedProject
            {
                ProjectName = name,
                ProjectPath = Path.Combine(Path.GetTempPath(), name),
                Entities = [.. entities],
            },
        };

    private static ExtractedEntity Entity(string className, params ExtractedProperty[] properties) =>
        new()
        {
            ClassName = className,
            Namespace = "Fixture.Module.BusinessObjects",
            BaseType = "BaseObject",
            FilePath = Path.Combine(Path.GetTempPath(), "App", $"{className}.cs"),
            Line = 10,
            Properties = [.. properties],
        };

    private static ExtractedProperty Prop(string name, string typeName) =>
        new() { Name = name, TypeName = typeName };

    private static string Between(string text, string start, string end)
    {
        var from = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"The page never contains '{start}'.");

        var to = text.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to >= 0, $"'{start}' is never closed by '{end}'.");

        return text[from..to];
    }
}
