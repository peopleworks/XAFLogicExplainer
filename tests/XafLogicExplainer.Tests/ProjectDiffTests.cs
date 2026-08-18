using XafLogicExplainer.Core.Diff;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// What the diff reports when an appearance rule changes between two snapshots.
/// </summary>
/// <remarks>
/// The diff keyed appearance rules on <c>rule.Id</c> alone and collected them into a set — the same
/// collapse that <c>FoldInto</c> was fixed for, one file over, and reached the same way: once rules
/// are read off properties, an application holds several rules with no identifier and they all
/// become one entry.
/// <para>
/// It failed three ways from that one key. Adding or removing an unnamed rule reported nothing,
/// because the set already held its empty name. Editing a rule's criteria reported nothing, because
/// the identifier had not moved. And a rule changed from disabling a field to hiding it reported
/// nothing either, which is the whole of what an appearance rule does.
/// </para>
/// <para>
/// These are the first tests over <see cref="ProjectDiffEngine"/>, which is why the key survived.
/// </para>
/// </remarks>
public class ProjectDiffTests
{
    private static ExtractedProject App(params ExtractedAppearanceRule[] rules) => new()
    {
        ProjectName = "SampleApp",
        Entities = [new ExtractedEntity { ClassName = "Invoice", AppearanceRules = [.. rules] }],
    };

    private static ExtractedAppearanceRule Rule(
        string id = "", string? targets = null, string? criteria = null,
        string? enabled = null, string? visibility = null, string? inheritedFrom = null) => new()
    {
        Id = id,
        TargetItems = targets,
        Criteria = criteria,
        Enabled = enabled,
        Visibility = visibility,
        InheritedFrom = inheritedFrom,
    };

    private static (List<string> Added, List<string> Removed) Changes(
        ExtractedProject before, ExtractedProject after)
    {
        var report = new ProjectDiffEngine().Compare(before, after);

        return (report.EntityChanges.Modified.SelectMany(e => e.AddedAppearanceRules).ToList(),
                report.EntityChanges.Modified.SelectMany(e => e.RemovedAppearanceRules).ToList());
    }

    [Fact]
    public void AnUnnamedRuleAddedBesideAnotherUnnamedRuleIsReported()
    {
        // Two rules, no identifiers, different properties. Keyed on the identifier they are one
        // rule, so the application grew a rule and the report said nothing had happened.
        var before = App(Rule(targets: "ChangedBy", enabled: "false"));
        var after = App(Rule(targets: "ChangedBy", enabled: "false"),
                        Rule(targets: "AuditNotes", visibility: "Hide"));

        var (added, removed) = Changes(before, after);

        Assert.Equal("(unnamed) on AuditNotes always: visibility=Hide", Assert.Single(added));
        Assert.Empty(removed);
    }

    [Fact]
    public void AnUnnamedRuleRemovedFromBesideAnotherIsReported()
    {
        var before = App(Rule(targets: "ChangedBy", enabled: "false"),
                         Rule(targets: "AuditNotes", visibility: "Hide"));
        var after = App(Rule(targets: "ChangedBy", enabled: "false"));

        var (added, removed) = Changes(before, after);

        Assert.Equal("(unnamed) on AuditNotes always: visibility=Hide", Assert.Single(removed));
        Assert.Empty(added);
    }

    [Fact]
    public void EditingWhenARuleFiresIsReported()
    {
        // The identifier does not move when the criteria is rewritten, so this reported as an
        // application that had not changed -- while the condition governing the field had.
        var before = App(Rule("Invoice_LockPosted", "Total", "Status = 'Posted'", enabled: "false"));
        var after = App(Rule("Invoice_LockPosted", "Total", "Status <> 'Draft'", enabled: "false"));

        var (added, removed) = Changes(before, after);

        Assert.Equal("Invoice_LockPosted on Total when Status <> 'Draft': enabled=false", Assert.Single(added));
        Assert.Equal("Invoice_LockPosted on Total when Status = 'Posted': enabled=false", Assert.Single(removed));
    }

    [Fact]
    public void ChangingWhatARuleDoesIsReported()
    {
        // A field that used to be shown read-only is now not shown at all. Under the old key this
        // was the same rule as before, so nobody reading the diff would learn the field had gone.
        var before = App(Rule("Invoice_LockPosted", "Total", "Status = 'Posted'", enabled: "false"));
        var after = App(Rule("Invoice_LockPosted", "Total", "Status = 'Posted'", visibility: "Hide"));

        var (added, removed) = Changes(before, after);

        Assert.Equal("Invoice_LockPosted on Total when Status = 'Posted': visibility=Hide", Assert.Single(added));
        Assert.Equal("Invoice_LockPosted on Total when Status = 'Posted': enabled=false", Assert.Single(removed));
    }

    [Fact]
    public void AnApplicationThatDidNotChangeReportsNothing()
    {
        // The other half of a richer key: one that varies with anything at all would report every
        // extraction as a rewrite, which is a worse failure than the silence it replaced.
        var rules = new[]
        {
            Rule("Invoice_LockPosted", "Total", "Status = 'Posted'", enabled: "false"),
            Rule(targets: "ChangedBy", enabled: "false"),
            Rule(targets: "AuditNotes", visibility: "Hide"),
        };

        var (added, removed) = Changes(App(rules), App(rules));

        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void ARuleInheritedFromABaseIsStillNotReportedUnderTheDescendant()
    {
        // The declared-rather-than-folded policy, which the richer key must not quietly undo: a
        // change has one author, and reporting it again under every entity in the hierarchy would
        // bury the line that says where it was actually made.
        var before = App(Rule(targets: "ChangedBy", enabled: "false", inheritedFrom: "AuditedObject"));
        var after = App(Rule(targets: "ChangedBy", visibility: "Hide", inheritedFrom: "AuditedObject"));

        var (added, removed) = Changes(before, after);

        Assert.Empty(added);
        Assert.Empty(removed);
    }
}
