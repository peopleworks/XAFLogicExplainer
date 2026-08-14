using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>An entity whose own columns are outnumbered by the ones it inherits.</summary>
[DefaultClassOptions]
[NavigationItem("Billing")]
public class Invoice : AuditedObject
{
    public Invoice(Session session) : base(session) { }

    [RuleRequiredField("Invoice_Number_Required", DefaultContexts.Save)]
    public string Number
    {
        get => GetPropertyValue<string>(nameof(Number));
        set => SetPropertyValue(nameof(Number), value);
    }

    public DateTime IssuedOn
    {
        get => GetPropertyValue<DateTime>(nameof(IssuedOn));
        set => SetPropertyValue(nameof(IssuedOn), value);
    }

    public decimal Total
    {
        get => GetPropertyValue<decimal>(nameof(Total));
        set => SetPropertyValue(nameof(Total), value);
    }
}
