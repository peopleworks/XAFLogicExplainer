using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>The other end of an association an entity declares for itself.</summary>
[DefaultClassOptions]
public class InvoiceLine : BaseObject
{
    public InvoiceLine(Session session) : base(session) { }

    public string Description
    {
        get => GetPropertyValue<string>(nameof(Description));
        set => SetPropertyValue(nameof(Description), value);
    }

    [Association("Invoice-Lines")]
    public Invoice Invoice
    {
        get => GetPropertyValue<Invoice>(nameof(Invoice));
        set => SetPropertyValue(nameof(Invoice), value);
    }
}
