using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Appearance rules that were not given an identifier.
/// </summary>
/// <remarks>
/// A rule written on a property does not need a name — the property is what it governs — and the
/// DevExpress non-persistent-objects demo writes exactly that: <c>[Appearance("", Enabled = false,
/// TargetItems = "*")]</c>. So an empty id is ordinary, not a mistake.
/// <para>
/// The fold that carries a base class's rules down to its descendants keys appearance rules on
/// <c>rule.Id</c> alone, and <see cref="FoldInto"/> skips a key it has already seen. Two unnamed
/// rules therefore look like one rule to it, and the second is dropped without a word.
/// <c>ValidationRuleKey</c> already guards against this — it falls back to the attribute and the
/// property when a rule has no id, "because two unnamed rules of the same kind on the same property
/// cannot be told apart anyway". Appearance rules had no such fallback.
/// </para>
/// <para>
/// Reading rules off properties is what made this reachable: before, appearance rules came only
/// from the class, where one unnamed rule per class is the most anyone writes.
/// </para>
/// </remarks>
public class UnnamedAppearanceRuleTests
{
    private static List<ExtractedAppearanceRule> InheritedByInvoice => SampleProjects.AuditedXpo
        .Entity("Invoice").AppearanceRules
        .Where(rule => string.IsNullOrEmpty(rule.Id))
        .ToList();

    [Fact]
    public void BothUnnamedRulesSurviveTheFoldIntoADescendant()
    {
        Assert.Equal(2, InheritedByInvoice.Count);
    }

    [Fact]
    public void EachKeepsThePropertyItGoverns()
    {
        Assert.Equal(
            new[] { "AuditNotes", "ChangedBy" },
            InheritedByInvoice.Select(rule => rule.TargetItems).OrderBy(name => name).ToArray());
    }

    [Fact]
    public void TheyAreStillDistinguishableByWhatTheyDo()
    {
        // Dropping one of the two would leave the survivor looking like the whole truth, so the
        // reader would be told the audit notes are hidden but not that ChangedBy is read-only, or
        // the reverse -- with nothing to indicate a rule had gone missing.
        var byProperty = InheritedByInvoice.ToDictionary(rule => rule.TargetItems!);

        Assert.Equal("false", byProperty["ChangedBy"].Enabled);
        Assert.Equal("Hide", byProperty["AuditNotes"].Visibility);
    }
}
