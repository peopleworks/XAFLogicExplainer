using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Wiki;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Two business classes of one name, in two namespaces of one application (#84).
/// </summary>
/// <remarks>
/// XAF accepts the shape once one of the classes is given its own view id prefix. The explain page
/// threw on it, because the map kept its entities in a dictionary keyed by the bare class name, and
/// every other place that looked a class up that way picked one of the two without saying so.
/// </remarks>
public class SharedClassNameTests
{
    private const string ReaderTag = "Library.Module.BusinessObjects.Tag";
    private const string CatalogTag = "Library.Module.BusinessObjects.Catalog.Tag";

    [Fact]
    public void BothClassesAreListed() =>
        Assert.Equal(
            new[] { CatalogTag, ReaderTag },
            Library.Entities
                .Where(entity => entity.ClassName == "Tag")
                .Select(entity => $"{entity.Namespace}.{entity.ClassName}")
                .Order(StringComparer.Ordinal));

    [Fact]
    public void TheExplainPageIsWritten() =>
        Assert.EndsWith("</html>", Page.TrimEnd());

    [Fact]
    public void EachClassHasACardOfItsOwn()
    {
        Assert.Contains($"id=\"entity-{ReaderTag}\"", Page);
        Assert.Contains($"id=\"entity-{CatalogTag}\"", Page);
    }

    [Fact]
    public void AClassWhoseNameIsItsOwnKeepsTheAnchorItAlwaysHad() =>
        Assert.Contains("id=\"entity-Book\"", Page);

    [Fact]
    public void TheMapDrawsEachClassOnceAndLabelsTheTwoApart()
    {
        Assert.Equal(4, Graph.Nodes.Count);
        Assert.Equal(
            new[] { "Book", "BusinessObjects.Tag", "Catalog.Tag", "Entry" },
            Graph.Nodes.Select(node => node.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AReferenceWrittenWithItsNamespaceIsDrawn() =>
        Assert.Contains(Graph.Edges, edge =>
            edge.From.Name == "Book" && edge.To.Name == "Catalog.Tag" && edge.Label == "Genres");

    [Fact]
    public void EachClassNamedTagKeepsItsOwnRelationships()
    {
        Assert.Contains(Graph.Edges, edge =>
            edge.From.Name == "BusinessObjects.Tag" && edge.To.Name == "Book" && edge.Label == "Books");
        Assert.Contains(Graph.Edges, edge =>
            edge.From.Name == "Catalog.Tag" && edge.To.Name == "Entry" && edge.IsAggregated);
        Assert.DoesNotContain(Graph.Edges, edge =>
            edge.From.Name == "BusinessObjects.Tag" && edge.Label == "Entries");
    }

    [Fact]
    public void ABareNameMeansTheClassInTheNamespaceItIsWrittenIn()
    {
        Assert.Contains($"href=\"#entity-{CatalogTag}\">Tag</a>", Card("Entry"));
        Assert.Contains($"href=\"#entity-{ReaderTag}\">Tag</a>", Card("Book"));
    }

    [Theory]
    [InlineData("Tag_ListView")]
    [InlineData("Tag_Books_ListView")]
    [InlineData("CatalogTag_ListView")]
    [InlineData("CatalogTag_DetailView")]
    [InlineData("CatalogTag_LookupListView")]
    [InlineData("CatalogTag_Books_ListView")]
    [InlineData("CatalogTag_Entries_ListView")]
    public void EachClassGetsTheViewIdsXafGeneratesForIt(string id) =>
        Assert.Contains(id, Library.Views.Select(view => view.Id));

    [Fact]
    public void ACollectionOfTheCatalogsTagHasItsNestedView() =>
        Assert.Equal("Tag", Assert.Single(Library.Views, view => view.Id == "Book_Genres_ListView").ObjectType);

    [Fact]
    public void AViewUnderItsPrefixIsCustomizedRatherThanHandWritten() =>
        Assert.Equal(ViewOrigin.Customized, Assert.Single(Library.Views, view => view.Id == "CatalogTag_ListView").Origin);

    [Fact]
    public void AnIdPrefixIsNotARegistration() =>
        Assert.DoesNotContain(Library.ModuleInfo!.RegisteredTypes, type => type.EndsWith("Tag", StringComparison.Ordinal));

    [Fact]
    public void TheAgentIndexNamesEachClassApart()
    {
        var index = new AgentContextGenerator().GenerateIndex(Library, []);

        Assert.Contains("| **BusinessObjects.Tag** ", index);
        Assert.Contains("| **Catalog.Tag** ", index);
        Assert.Contains("`BusinessObjects.Tag`", Row(index, "Book"));
        Assert.Contains("`Catalog.Tag`", Row(index, "Book"));
    }

    [Fact]
    public void TheMarkdownDocumentationIsWritten() =>
        Assert.NotEmpty(new MarkdownDocumentationGenerator("en").GenerateSections(Library));

    [Fact]
    public void TheWikiGivesEachClassACardOfItsOwn()
    {
        var html = new WikiGenerator("0.0.0-test").Generate(CorpusAnalyzer.Analyze(
        [
            new WikiApplication { Name = "Library", Slug = "library", Project = Library },
        ]));

        Assert.Contains($"id=\"library-entity-{ReaderTag}\"", html);
        Assert.Contains($"id=\"library-entity-{CatalogTag}\"", html);
    }

    [Fact]
    public void TwoReportDialogsOfOneNameAreBothKept() =>
        Assert.Equal(2, Library.UnregisteredReportParameters.Count(parameters => parameters.ClassName == "LabelParameters"));

    private static ExtractedProject Library => SampleProjects.SharedClassName;

    private static EntityGraph Graph => EntityGraph.Build(Library);

    private static readonly Lazy<string> LazyPage = new(() => new HtmlExplainerGenerator("0.0.0-test").Generate(Library));

    private static string Page => LazyPage.Value;

    /// <summary>One entity's card on the explain page, from its anchor to the end of the card.</summary>
    private static string Card(string anchor)
    {
        var start = Page.IndexOf($"id=\"entity-{anchor}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The page has no card anchored at '{anchor}'.");

        return Page[start..Page.IndexOf("</article>", start, StringComparison.Ordinal)];
    }

    private static string Row(string index, string label) =>
        index.Replace("\r", "").Split('\n').Single(line => line.StartsWith($"| **{label}** ", StringComparison.Ordinal));
}
