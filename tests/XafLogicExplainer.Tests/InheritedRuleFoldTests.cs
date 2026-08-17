using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Rules and associations an entity inherits from a class declared in the same project.
/// </summary>
/// <remarks>
/// Folding properties (<see cref="InheritedPropertyFoldTests"/>) carried what lives on a property
/// but not what lives on the class. A <c>RuleCriteria</c> on an audit base is enforced every time
/// any entity in the application is saved and appeared under the base alone, so a reader told the
/// inventories were complete read an entity's section and learned of no rule — issue #14.
/// <para>
/// The other half of that issue is what a rule set, a diagram or a count should say once every
/// entity carries its ancestors' declarations. Those answer a different question than an entity's
/// own section does, and are pinned in <see cref="DeclaredRatherThanFoldedTests"/>.
/// </para>
/// </remarks>
public class InheritedRuleFoldTests
{
    private static ExtractedEntity Receipt => SampleProjects.AuditedXpo.Entity("Receipt");
    private static ExtractedEntity CreditNote => SampleProjects.AuditedXpo.Entity("CreditNote");

    [Fact]
    public void AnEntityCarriesTheRulesEnforcedWhenItIsSaved()
    {
        // Receipt declares no rule of its own and is governed by two.
        Assert.Equal(
            ["Audit_ChangedNotBeforeCreated", "Audit_CreatedByRequired"],
            Receipt.ValidationRules.Select(rule => rule.Id).Order());
    }

    [Fact]
    public void AnEntityCarriesTheAppearanceRulesThatStyleIt()
    {
        var appearance = Assert.Single(Receipt.AppearanceRules);

        Assert.Equal("Audit_ReadOnlyOnceVersioned", appearance.Id);
        Assert.Equal("RowVersion > 0", appearance.Criteria);
    }

    [Fact]
    public void AnEntityCarriesTheAssociationsItInherits()
    {
        // The collection property folded with the rest; the association behind it did not, so the
        // entity listed a collection whose other end was recorded nowhere on it.
        var association = Assert.Single(Receipt.Relationships);

        Assert.Equal("AuditEntries", association.PropertyName);
        Assert.Equal("AuditEntry", association.RelatedEntity);
        Assert.True(association.IsAggregated);
    }

    [Fact]
    public void EachFoldedDeclarationNamesTheClassThatWroteIt()
    {
        Assert.Equal("AuditedObject", Receipt.ValidationRules.First().InheritedFrom);
        Assert.Equal("AuditedObject", Assert.Single(Receipt.AppearanceRules).InheritedFrom);
        Assert.Equal("AuditedObject", Assert.Single(Receipt.Relationships).InheritedFrom);
    }

    [Fact]
    public void ARuleArrivesFromAGrandparentToo()
    {
        // CreditNote reaches the audit base through Invoice, and the depth is where a fold that
        // only looked one hop up would quietly stop.
        var audit = CreditNote.ValidationRules.Single(rule => rule.Id == "Audit_ChangedNotBeforeCreated");

        Assert.Equal("AuditedObject", audit.InheritedFrom);
    }

    [Fact]
    public void RedeclaringARuleReplacesItRatherThanRepeatingIt()
    {
        // A credit note is an invoice whose total runs the other way. Both classes write
        // Invoice_TotalPositive, and listing both would show a reader two rules that contradict
        // each other, one of which is not in force.
        var total = Assert.Single(CreditNote.ValidationRules, rule => rule.Id == "Invoice_TotalPositive");

        Assert.Equal("Total < 0", total.Expression);
        Assert.Null(total.InheritedFrom);
    }

    [Fact]
    public void TheClassThatDeclaresARuleStillOwnsIt()
    {
        // The base must not lose its own to the fold: a rule marked as inherited everywhere,
        // including where it was written, is the same defect facing the other way.
        var audit = SampleProjects.AuditedXpo.Entity("AuditedObject").ValidationRules
            .Single(rule => rule.Id == "Audit_ChangedNotBeforeCreated");

        Assert.Null(audit.InheritedFrom);
    }

    [Fact]
    public void AnEntitysSectionSaysWhichRulesAreItsOwn()
    {
        // Folding without marking would be the defect traded for a quieter one: the reader now
        // sees every rule that governs the entity and cannot tell which of them changing would
        // change the whole application.
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.AuditedXpo);
        var entities = sections.Single(section => section.Content.Contains("# SampleAudited - Business Entities"));

        var receipt = entities.Content
            .Replace("\r", "")
            .Split("\n## ", StringSplitOptions.None)
            .Single(block => block.StartsWith("Receipt\n", StringComparison.Ordinal));

        Assert.Contains("Audit_ChangedNotBeforeCreated", receipt, StringComparison.Ordinal);
        Assert.Contains("inherited from `AuditedObject`", receipt, StringComparison.Ordinal);
    }
}
