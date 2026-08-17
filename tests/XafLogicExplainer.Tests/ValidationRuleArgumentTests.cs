using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// What a rule attribute's arguments mean, when they were passed by position.
/// </summary>
/// <remarks>
/// Every fixture wrote its message as <c>CustomMessageTemplate =</c>, so the four-argument
/// overload — <c>("id", DefaultContexts.Save, criteria, message)</c> — appeared nowhere and the
/// whole suite agreed that the last positional literal was the criteria. On that overload the last
/// literal is the message, and the field meant to hold what the rule enforces held the sentence
/// explaining it to the user instead.
/// </remarks>
public class ValidationRuleArgumentTests
{
    private static ExtractedValidationRule AuditRule => SampleProjects.AuditedXpo
        .Entity("AuditedObject").ValidationRules
        .Single(rule => rule.RuleType.Contains("Criteria", StringComparison.Ordinal));

    [Fact]
    public void TheCriteriaIsTheCriteriaAndNotTheMessageThatFollowsIt()
    {
        Assert.Equal("ChangedOn >= CreatedOn", AuditRule.Expression);
    }

    [Fact]
    public void AMessagePassedByPositionIsStillTheMessage()
    {
        // It was dropped: only the named form set it, so a rule written the other way was
        // documented with no message at all -- while its text sat in the criteria field.
        Assert.Equal("A record cannot be changed before it was created.", AuditRule.MessageTemplate);
    }

    [Fact]
    public void ARuleKnowsWhatItIsCalled()
    {
        // The identifier a validation error carries and the Model Editor lists it under. It was
        // read into the raw parameter bag as `arg0`, which is how the documents printed it.
        Assert.Equal("Audit_ChangedNotBeforeCreated", AuditRule.Id);

        var required = SampleProjects.AuditedXpo.Entity("Invoice").ValidationRules
            .Single(rule => rule.RuleType.Contains("Required", StringComparison.Ordinal)
                            && rule.InheritedFrom is null);

        Assert.Equal("Invoice_Number_Required", required.Id);
    }

    [Fact]
    public void ARuleKnowsWhereItRuns()
    {
        // Recorded as written -- resolving the enum would mean compiling.
        Assert.Equal("DefaultContexts.Save", AuditRule.Contexts);
    }

    [Fact]
    public void TheNamedFormStillWins()
    {
        // The overload the fixtures did use, which must not regress: criteria positional, message
        // named. Reading positions more eagerly is only safe if named arguments still take
        // precedence over anything inferred from a slot.
        var invoice = SampleProjects.AuditedXpo.Entity("Invoice").ValidationRules
            .Single(rule => rule.RuleType.Contains("Criteria", StringComparison.Ordinal)
                            && rule.InheritedFrom is null);

        Assert.Equal("Total > 0", invoice.Expression);
        Assert.Equal("An invoice must total more than zero.", invoice.MessageTemplate);
    }

    [Fact]
    public void ALoneArgumentIsTheRulesOwnAndNotAnIdentifier()
    {
        // [RuleCriteria("Total >= 0")] has no room for an id before the criteria. Reading slot 0
        // as an identifier unconditionally would name the rule after the expression and then lose
        // the expression, which is the same defect twice.
        var order = SampleProjects.Xpo.Entity("Order").ValidationRules
            .Single(rule => rule.RuleType.Contains("Criteria", StringComparison.Ordinal));

        Assert.Equal("Order_TotalNotNegative", order.Id);
        Assert.Equal("Total >= 0", order.Expression);
    }

    [Fact]
    public void TheDocumentsNameTheRuleRatherThanItsArgumentList()
    {
        // The rendering that made this visible: with nothing in the fields, every generator fell
        // back to dumping the attribute's arguments, so the published documentation described the
        // application's validation as `arg0=..., arg1=DefaultContexts.Save`.
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.AuditedXpo);

        foreach (var section in sections)
            Assert.DoesNotContain("arg0=", section.Content, StringComparison.Ordinal);

        var rules = sections.Single(section => section.Content.Contains("# SampleAudited - Business Rules"));

        Assert.Contains("`Audit_ChangedNotBeforeCreated`", rules.Content, StringComparison.Ordinal);
        Assert.Contains("`ChangedOn >= CreatedOn`", rules.Content, StringComparison.Ordinal);
    }
}
