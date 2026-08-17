using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>The other end of the association the audit base gives every entity.</summary>
[DefaultClassOptions]
public class AuditEntry : BaseObject
{
    public AuditEntry(Session session) : base(session) { }

    public string Action
    {
        get => GetPropertyValue<string>(nameof(Action));
        set => SetPropertyValue(nameof(Action), value);
    }

    [Association("AuditedObject-AuditEntries")]
    public AuditedObject Target
    {
        get => GetPropertyValue<AuditedObject>(nameof(Target));
        set => SetPropertyValue(nameof(Target), value);
    }
}
