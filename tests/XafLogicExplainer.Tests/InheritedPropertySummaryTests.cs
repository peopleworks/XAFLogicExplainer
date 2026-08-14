using System.Text.RegularExpressions;
using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Tests;

/// <summary>
/// What a summary of fixed width names once an entity carries its base's columns.
/// </summary>
/// <remarks>
/// Folding inherited properties (<see cref="InheritedPropertyFoldTests"/>) gives every entity the
/// columns it persists, in the order the class reads them — root down. Two summaries then take the
/// first few of that list, and on an application with a shared audit base the first few are the
/// base's for every entity at once: the tables that exist to tell entities apart end up naming the
/// same columns in every row, and none of the ones that differ.
/// <para>
/// The full listings are unaffected and still read root down. This is only about the summaries,
/// where the width is the whole constraint.
/// </para>
/// </remarks>
public class InheritedPropertySummaryTests
{
    private static string Index =>
        new AgentContextGenerator("0.9.0").GenerateIndex(SampleProjects.AuditedXpo, []);

    [Fact]
    public void AnEntitysOwnColumnsSurviveAFixedWidthSummary()
    {
        // Receipt declares two columns and inherits six. Ranked by nothing but position, the six
        // fill all five slots and the reader learns that a receipt has a CreatedBy.
        Assert.Equal(["Reference", "Amount", "CreatedBy", "CreatedOn", "ChangedBy"], Notable("Receipt"));
    }

    [Fact]
    public void RequiredColumnsStillOutrankTheRestOfTheEntitysOwn()
    {
        // Own-before-inherited is the outer sort; the existing ranking still orders each group.
        Assert.Equal(["Number", "IssuedOn", "Total", "CreatedBy", "CreatedOn"], Notable("Invoice"));
    }

    [Fact]
    public void TheBaseItselfStillNamesItsOwnColumns()
    {
        // The base declares all six, so the rule must not be "hide inherited" -- it is "declare
        // first". A base whose row went blank would be the same defect facing the other way.
        Assert.Equal(["CreatedBy", "CreatedOn", "ChangedBy", "ChangedOn", "RowVersion"], Notable("AuditedObject"));
    }

    [Fact]
    public void TheNavigationSummaryNamesTheEntitysOwnColumnsFirstToo()
    {
        // The same eight-slot summary, in the documents published to a Copilot rather than to a
        // coding agent -- one fix per reader, or the reader who gets missed keeps the defect.
        // Generated in the default language, which is Spanish; the ordering is not translated.
        var sections = new MarkdownDocumentationGenerator().GenerateSections(SampleProjects.AuditedXpo);
        var navigation = sections.Single(section => section.Content.Contains("#### Receipt"));

        var line = navigation.Content
            .Split('\n')
            .SkipWhile(text => !text.StartsWith("#### Receipt", StringComparison.Ordinal))
            .First(text => text.Contains("Propiedades principales", StringComparison.Ordinal));

        Assert.StartsWith(
            "- **Propiedades principales:** Reference, Amount, CreatedBy",
            line.Trim(),
            StringComparison.Ordinal);
    }

    /// <summary>The property names in one row of the agent context's entity table.</summary>
    private static List<string> Notable(string className)
    {
        var row = Index
            .Split('\n')
            .Single(line => line.StartsWith($"| **{className}** ", StringComparison.Ordinal));

        return [.. Regex.Matches(row.Split('|')[3], "`([^`]+)`").Select(match => match.Groups[1].Value)];
    }
}
