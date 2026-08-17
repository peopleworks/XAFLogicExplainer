using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>
/// The audit base an application writes once and derives every entity from.
/// </summary>
/// <remarks>
/// Wider than the columns of the entities below it, which is the ordinary shape: audit bases
/// accumulate. It is what makes a fixed-size summary a decision about whose columns get named.
/// <para>
/// It also carries a rule, an appearance rule and an association, so that everything the base
/// imposes on the whole application is imposed here from one declaration — the shape that decides
/// whether a reader of any one entity is told the truth about it.
/// </para>
/// <para>
/// Its message is passed by position rather than as <c>CustomMessageTemplate =</c>, which is the
/// overload no other fixture writes.
/// </para>
/// </remarks>
[RuleCriteria("Audit_ChangedNotBeforeCreated", DefaultContexts.Save, "ChangedOn >= CreatedOn",
    "A record cannot be changed before it was created.")]
[Appearance("Audit_ReadOnlyOnceVersioned", Criteria = "RowVersion > 0", Enabled = false)]
public abstract class AuditedObject : BaseObject
{
    public AuditedObject(Session session) : base(session) { }

    [Association("AuditedObject-AuditEntries"), Aggregated]
    public XPCollection<AuditEntry> AuditEntries => GetCollection<AuditEntry>(nameof(AuditEntries));

    [RuleRequiredField("Audit_CreatedByRequired", DefaultContexts.Save)]
    public string CreatedBy
    {
        get => GetPropertyValue<string>(nameof(CreatedBy));
        set => SetPropertyValue(nameof(CreatedBy), value);
    }

    public DateTime CreatedOn
    {
        get => GetPropertyValue<DateTime>(nameof(CreatedOn));
        set => SetPropertyValue(nameof(CreatedOn), value);
    }

    // Two rules written the way the DevExpress non-persistent-objects demo writes them: the id
    // left empty, because a rule on a property already says what it governs.
    [Appearance("", Enabled = false)]
    public string ChangedBy
    {
        get => GetPropertyValue<string>(nameof(ChangedBy));
        set => SetPropertyValue(nameof(ChangedBy), value);
    }

    public DateTime ChangedOn
    {
        get => GetPropertyValue<DateTime>(nameof(ChangedOn));
        set => SetPropertyValue(nameof(ChangedOn), value);
    }

    public int RowVersion
    {
        get => GetPropertyValue<int>(nameof(RowVersion));
        set => SetPropertyValue(nameof(RowVersion), value);
    }

    [Appearance("", Visibility = "Hide")]
    public string AuditNotes
    {
        get => GetPropertyValue<string>(nameof(AuditNotes));
        set => SetPropertyValue(nameof(AuditNotes), value);
    }
}
