using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Tests;

/// <summary>
/// What the application-wide readings say, now that every entity carries its ancestors'
/// declarations.
/// </summary>
/// <remarks>
/// Folding answers the question a reader of one entity is asking: everything that governs it,
/// wherever it was written. An index, a count, a diagram and a search answer a different question
/// — what this application declares — and following the fold there would make every one of them a
/// measurement of the class hierarchy instead. One rule on an audit base is one rule, however many
/// classes are enforced by it.
/// <para>
/// This is the half of issue #14 filed as debatable, and the answer is that both renderings are
/// right about their own question. Marking every folded declaration with its declarer is what lets
/// each one choose.
/// </para>
/// </remarks>
public class DeclaredRatherThanFoldedTests
{
    [Fact]
    public void TheRuleIndexListsEachRuleOnceUnderTheClassThatWroteIt()
    {
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.AuditedXpo);
        var rules = sections.Single(section => section.Content.Contains("# SampleAudited - Business Rules"));

        var mentions = rules.Content
            .Split('\n')
            .Count(line => line.Contains("Audit_ChangedNotBeforeCreated", StringComparison.Ordinal));

        Assert.Equal(1, mentions);
    }

    [Fact]
    public void TheEntityMapDrawsOneArrowPerAssociationAndNotOnePerDescendant()
    {
        // Four classes inherit the audit base's association. Drawn from each of them, the map of a
        // real application becomes a picture of its base class.
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.AuditedXpo);
        var overview = sections.Single(section => section.Content.Contains("Entity Relationship Map"));

        var arrows = overview.Content
            .Split('\n')
            .Count(line => line.Contains("via `AuditEntries`", StringComparison.Ordinal));

        Assert.Equal(1, arrows);
    }

    [Fact]
    public void TheDiagramDrawsTheSchemaRatherThanTheHierarchy()
    {
        var graph = EntityGraph.Build(SampleProjects.AuditedXpo);

        Assert.Equal(1, graph.Edges.Count(edge => edge.To.Name == "AuditEntry"));
    }

    [Fact]
    public void CountsMeasureTheApplicationAndNotItsInheritanceDepth()
    {
        var html = new HtmlExplainerGenerator("0.13.0").Generate(SampleProjects.AuditedXpo);

        // Five validation rules and three appearance rules are written in this application.
        // Counting the folded copies reports twelve of each, on six entities -- a number that grows
        // when somebody adds a subclass and changes nothing about the validation.
        Assert.Contains("<b>5</b><span>validation rules", Compact(html), StringComparison.Ordinal);
        Assert.Contains("<b>3</b><span>appearance rules", Compact(html), StringComparison.Ordinal);

        // The folded totals the paragraph above names, enforced rather than asserted in prose --
        // they are the whole reason the page reports the declared numbers instead.
        var entities = SampleProjects.AuditedXpo.Entities;
        Assert.Equal(12, entities.Sum(e => e.ValidationRules.Count));
        Assert.Equal(12, entities.Sum(e => e.AppearanceRules.Count));
    }

    /// <summary>The page with its line breaks removed, so a stat can be matched across them.</summary>
    private static string Compact(string html) => html.Replace("\r", "").Replace("\n", "");
}
