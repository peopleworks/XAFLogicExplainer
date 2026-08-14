using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>
/// The audit base an application writes once and derives every entity from.
/// </summary>
/// <remarks>
/// Wider than the columns of the entities below it, which is the ordinary shape: audit bases
/// accumulate. It is what makes a fixed-size summary a decision about whose columns get named.
/// </remarks>
public abstract class AuditedObject : BaseObject
{
    public AuditedObject(Session session) : base(session) { }

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

    public string AuditNotes
    {
        get => GetPropertyValue<string>(nameof(AuditNotes));
        set => SetPropertyValue(nameof(AuditNotes), value);
    }
}
