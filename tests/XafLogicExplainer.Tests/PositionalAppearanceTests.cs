using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Walkthrough;
using XafLogicExplainer.Mcp;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Appearance rules written the way the attribute's constructors allow, rather than the way every
/// fixture happened to write them.
/// </summary>
/// <remarks>
/// <c>AppearanceAttribute</c> has three constructors and two of them pass the criteria by position:
/// <c>(id, criteria)</c> and <c>(id, appearanceItemType, criteria)</c>. Only the named form was
/// read, so a rule written either of the other two ways was extracted with no condition at all and
/// then documented as applying unconditionally — the strongest claim a rule can make, printed about
/// three rules whose entire purpose was a condition.
/// <para>
/// The suite agreed because every existing fixture wrote <c>Criteria =</c> named. That is the same
/// shape as the <c>CustomMessageTemplate</c> blind spot closed in #14: a form no fixture uses cannot
/// be seen to be missing.
/// </para>
/// <para>
/// <c>AppearanceItemType</c> was not read either, so a rule governing the <c>Delete</c> Action was
/// described as governing a field called Delete — a column no reader would ever find. Reported
/// together in #22 because the same reader has to change for both.
/// </para>
/// </remarks>
public class PositionalAppearanceTests
{
    private static ExtractedEntity Ticket => SampleProjects.Appearance.Entity("Ticket");

    private static ExtractedAppearanceRule Rule(string id) =>
        Ticket.AppearanceRules.FirstOrDefault(rule => rule.Id == id)
        ?? throw new InvalidOperationException(
            $"No rule '{id}'. Extracted: {string.Join(", ", Ticket.AppearanceRules.Select(r => r.Id))}");

    [Fact]
    public void CriteriaPassedAsTheSecondArgumentIsRead()
    {
        // [Appearance("Ticket_ClosedIsGrey", "Status = 'Closed'", FontColor = "Gray")]
        Assert.Equal("Status = 'Closed'", Rule("Ticket_ClosedIsGrey").Criteria);
        Assert.Equal("Gray", Rule("Ticket_ClosedIsGrey").FontColor);
    }

    [Fact]
    public void CriteriaPassedAsTheThirdArgumentIsRead()
    {
        // [Appearance("Ticket_LockActions", AppearanceItemType.Action, "Status = 'Closed'", …)]
        // The criteria is the last positional argument in both overloads that carry one, which is
        // what lets it be read without knowing which constructor was called.
        Assert.Equal("Status = 'Closed'", Rule("Ticket_LockActions").Criteria);
    }

    [Fact]
    public void CriteriaOnAPropertyRuleIsReadTheSameWay()
    {
        Assert.Equal("Status <> 'Closed'", Rule("Ticket_ResolutionWhenClosed").Criteria);
        Assert.Equal("Resolution", Rule("Ticket_ResolutionWhenClosed").TargetItems);
    }

    [Fact]
    public void TheItemTypeIsReadWhicheverWayItIsWritten()
    {
        // Positionally it is the enum member and the source text reads `AppearanceItemType.Action`;
        // by name the DevExpress examples write the plain string. Both mean one rule.
        Assert.Equal("Action", Rule("Ticket_LockActions").AppearanceItemType);
        Assert.Equal("LayoutItem", Rule("Ticket_UrgentLayout").AppearanceItemType);

        // Absent means the XAF default, which is ViewItem. Recorded as absent rather than filled in,
        // so the document can tell a rule that said nothing from one that said ViewItem.
        Assert.Null(Rule("Ticket_ClosedIsGrey").AppearanceItemType);
    }

    [Fact]
    public void ANamedArgumentStillWinsOverAPosition()
    {
        // The rule the validation side already follows. `Ticket_UrgentLayout` writes both its item
        // type and its criteria by name, and neither may be overwritten by a fallback.
        Assert.Equal("Priority = 'Urgent'", Rule("Ticket_UrgentLayout").Criteria);
        Assert.Equal("EscalationGroup", Rule("Ticket_UrgentLayout").TargetItems);
    }

    [Fact]
    public void NoRuleIsDocumentedAsUnconditionalWhenItHasACondition()
    {
        // The defect as a reader met it. `always` is the strongest claim a rule can make, and it was
        // being printed about every one of these.
        var markdown = string.Join("\n", new MarkdownDocumentationGenerator("en")
            .GenerateSections(SampleProjects.Appearance)
            .Select(section => section.Content));

        Assert.Contains("when `Status = 'Closed'`", markdown, StringComparison.Ordinal);
        Assert.Contains("when `Status <> 'Closed'`", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Ticket_ClosedIsGrey** — always", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleOverAnActionIsNotCalledAField()
    {
        var markdown = string.Join("\n", new MarkdownDocumentationGenerator("en")
            .GenerateSections(SampleProjects.Appearance)
            .Select(section => section.Content));

        Assert.Contains("actions: Delete", markdown, StringComparison.Ordinal);
        Assert.Contains("layout items: EscalationGroup", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("fields: Delete", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheExpressionIndexStopsClaimingCompletenessItDoesNotHave()
    {
        // The page introduces itself as every distinct expression in the application, so a missing
        // one cannot be told from an absent one. Nothing here changed but the extraction: the index
        // already gathered appearance criteria and there were none to gather.
        var conventions = CodebaseConventions.Infer(SampleProjects.Appearance);

        Assert.Contains(conventions.CriteriaExamples, example =>
            example.Expression.Contains("Status = 'Closed'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnAgentIsToldTheConditionAndWhatKindOfThingItGoverns()
    {
        var tools = new XafDetailTools(new XafProjectContext(
            [new XafProjectSource { Name = "Helpdesk", Path = SampleProjects.AppearancePath, Language = "en" }]));

        var answer = await tools.EntityAsync("Ticket", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Status = 'Closed'", answer, StringComparison.Ordinal);
        Assert.Contains("Delete (actions)", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("applies: always", answer, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDiffSeesARuleRepointedFromAColumnToAnAction()
    {
        // Two rules that differ only in what kind of thing they govern are two different rules, and
        // nothing else in the diff's key would have moved.
        var previous = SampleProjects.Extract(SampleProjects.AppearancePath);
        var current = SampleProjects.Extract(SampleProjects.AppearancePath);

        current.Entity("Ticket").AppearanceRules
            .First(rule => rule.Id == "Ticket_LockActions").AppearanceItemType = "ViewItem";

        var change = Assert.Single(
            WalkthroughDiff.Between(previous, current, "Ticket").Changes);

        Assert.Equal(ProcessChangeKind.Changed, change.Kind);
        Assert.Equal("Ticket_LockActions", change.Node.Name);
    }
}
