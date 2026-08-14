using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>
/// The same shape without a required column.
/// </summary>
/// <remarks>
/// <c>Invoice</c> keeps one of its own columns in a five-slot summary by accident, because a
/// required property outranks the rest. An entity with no such column keeps none — which is the
/// case that shows the ranking was never the thing holding the row together.
/// </remarks>
[DefaultClassOptions]
[NavigationItem("Billing")]
public class Receipt : AuditedObject
{
    public Receipt(Session session) : base(session) { }

    public string Reference
    {
        get => GetPropertyValue<string>(nameof(Reference));
        set => SetPropertyValue(nameof(Reference), value);
    }

    public decimal Amount
    {
        get => GetPropertyValue<decimal>(nameof(Amount));
        set => SetPropertyValue(nameof(Amount), value);
    }
}
