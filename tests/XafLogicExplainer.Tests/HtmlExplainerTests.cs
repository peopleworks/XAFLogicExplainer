using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The self-contained page a person reads.
/// </summary>
public class HtmlExplainerTests
{
    private static string Page => new HtmlExplainerGenerator("0.10.1").Generate(SampleProjects.Xpo);

    [Fact]
    public void IsASelfContainedDocument()
    {
        var html = Page;

        Assert.StartsWith("<!doctype html>", html);
        Assert.Contains("</html>", html);
        Assert.Contains("<style>", html);
        Assert.Contains("<script>", html);
    }

    [Fact]
    public void AsksNothingOfTheNetwork()
    {
        // It has to open from an email attachment, a network share, and a machine with no
        // internet — so every reference to somewhere else is a defect, not a nicety.
        var html = Page;

        Assert.DoesNotContain("<script src=", html);
        Assert.DoesNotContain("<link rel=\"stylesheet\"", html);
        Assert.DoesNotContain("@import", html);
        Assert.DoesNotContain("http://fonts", html);
        Assert.DoesNotContain("https://cdn", html);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Order")]
    [InlineData("OrderLine")]
    public void ShowsEveryEntity(string entity)
    {
        var html = Page;

        Assert.Contains($"id=\"entity-{entity}\"", html);
        Assert.Contains(entity, html);
    }

    [Fact]
    public void ShowsWhatAnActionRuns()
    {
        var html = Page;

        Assert.Contains("ApproveOrderController", html);
        Assert.Contains("ApproveOrder", html);
        Assert.Contains("IsApproved = true", html);
    }

    [Fact]
    public void ShowsTheRulesAUserWillHit()
    {
        var html = Page;

        Assert.Contains("A customer must have a name.", html);
        Assert.Contains("An order total cannot be negative.", html);
    }

    [Fact]
    public void ShowsCriteriaAndCalculations()
    {
        var html = Page;

        Assert.Contains("IsBlocked = False", html);
        Assert.Contains("Lines.Sum(LineTotal)", html);
    }

    [Fact]
    public void ShowsModelEditorCustomizationsAsInvisibleInCode()
    {
        var html = Page;

        Assert.Contains("Model Editor", html);
        Assert.Contains("only in XML", html);
        Assert.Contains("Order_ListView", html);
    }

    [Fact]
    public void EscapesSourceCodeIntoTheDocument()
    {
        // Method bodies and generic types are full of angle brackets. One unescaped body would
        // silently swallow the rest of the page, and the failure looks like missing content
        // rather than like a bug.
        var html = Page;

        Assert.Contains("XPCollection&lt;OrderLine&gt;", html);
        Assert.DoesNotContain("<OrderLine>", html);
    }

    [Fact]
    public void GivesEveryCardSomethingToSearch()
    {
        var html = Page;
        var cards = System.Text.RegularExpressions.Regex.Matches(html, "data-search=\"");

        Assert.True(cards.Count >= 5, $"only {cards.Count} searchable cards");
        Assert.Contains("data-search=\"customer", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DrawsTheDomainModel()
    {
        var html = Page;

        Assert.Contains("<svg viewBox=", html);
        Assert.Contains("class=\"node\" data-name=\"Order\"", html);
        Assert.Contains("class=\"edge", html);
    }

    // ------------------------------------------------------------ the graph

    [Fact]
    public void GraphPlacesEveryEntity()
    {
        var graph = EntityGraph.Build(SampleProjects.Xpo);

        Assert.Equal(3, graph.Nodes.Count);
        Assert.All(graph.Nodes, n => Assert.InRange(n.X, 0, graph.Width));
        Assert.All(graph.Nodes, n => Assert.InRange(n.Y, 0, graph.Height));
    }

    [Fact]
    public void GraphMarksOwnership()
    {
        var graph = EntityGraph.Build(SampleProjects.Xpo);

        var owned = Assert.Single(graph.Edges, e => e.IsAggregated);
        Assert.Equal("Order", owned.From.Name);
        Assert.Equal("OrderLine", owned.To.Name);
    }

    [Fact]
    public void GraphDrawsEachAssociationOnce()
    {
        // An association is declared at both ends. Drawing both produces a doubled line that
        // reads as two relationships.
        var graph = EntityGraph.Build(SampleProjects.Xpo);

        var betweenOrderAndCustomer = graph.Edges
            .Count(e => (e.From.Name == "Order" && e.To.Name == "Customer")
                     || (e.From.Name == "Customer" && e.To.Name == "Order"));

        Assert.Equal(1, betweenOrderAndCustomer);
    }

    [Fact]
    public void GraphIsDeterministic()
    {
        // Regenerating an explainer should produce a readable diff, not a reshuffled diagram.
        var first = EntityGraph.Build(SampleProjects.Xpo);
        var second = EntityGraph.Build(SampleProjects.Xpo);

        Assert.Equal(
            first.Nodes.Select(n => (n.Name, n.X, n.Y)),
            second.Nodes.Select(n => (n.Name, n.X, n.Y)));
    }

    [Fact]
    public void GraphSizesItsCanvasToTheModel()
    {
        // A fixed canvas leaves three entities floating in white space.
        var small = EntityGraph.Build(SampleProjects.EfCore);
        var larger = EntityGraph.Build(SampleProjects.Xpo);

        Assert.True(small.Height <= larger.Height);
        Assert.True(small.Width >= 560);
    }

    [Fact]
    public void GraphSurvivesAnApplicationWithNoEntities()
    {
        var empty = EntityGraph.Build(new Core.Models.ExtractedProject { ProjectName = "Empty" });

        Assert.True(empty.IsEmpty);
        Assert.Empty(empty.Edges);
    }

    // ------------------------------------------------- extraction regression

    [Fact]
    public void ExplicitInterfaceImplementationsAreNotSeparateProperties()
    {
        // `object INamedRecord.Name => Name;` is the same property seen through an interface.
        // Reporting both put a duplicate row in every rendering, the second one typed `object`,
        // which reads as a modelling mistake the team did not make.
        var customer = SampleProjects.Xpo.Entity("Customer");

        Assert.Single(customer.Properties, p => p.Name == "Name");
        Assert.DoesNotContain(customer.Properties, p => p.TypeName == "object");
    }
}
