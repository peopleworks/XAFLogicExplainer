using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleAudited.Module.BusinessObjects;

/// <summary>
/// An entity two levels down, which redeclares one of the rules above it.
/// </summary>
/// <remarks>
/// A credit note is an invoice whose total runs the other way, so it reuses the identifier and
/// replaces the criteria — which is how XAF is told this rule is the same rule, differently. It is
/// also the depth at which a rule has to arrive from a grandparent rather than a parent.
/// </remarks>
[DefaultClassOptions]
[NavigationItem("Billing")]
[RuleCriteria("Invoice_TotalPositive", DefaultContexts.Save, "Total < 0",
    CustomMessageTemplate = "A credit note must total less than zero.")]
public class CreditNote : Invoice
{
    public CreditNote(Session session) : base(session) { }

    public string Reason
    {
        get => GetPropertyValue<string>(nameof(Reason));
        set => SetPropertyValue(nameof(Reason), value);
    }
}
