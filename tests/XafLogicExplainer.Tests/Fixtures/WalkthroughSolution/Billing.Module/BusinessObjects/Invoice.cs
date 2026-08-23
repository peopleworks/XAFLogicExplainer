using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace Billing.Module.BusinessObjects;

/// <summary>
/// A bill sent to a customer.
/// </summary>
[DefaultClassOptions]
[Description("A bill sent to a customer.")]
public class Invoice : XPCustomObject
{
    public Invoice(Session session) : base(session) { }

    private decimal _total;

    [RuleValueComparison("Invoice_TotalNotNegative", DefaultContexts.Save,
        ValueComparisonType.GreaterThanOrEqual, 0)]
    public decimal Total
    {
        get => _total;
        set => SetPropertyValue(nameof(Total), ref _total, value);
    }

    private CreditNote _creditNote;

    [Association("Invoice-CreditNotes")]
    public CreditNote CreditNote
    {
        get => _creditNote;
        set => SetPropertyValue(nameof(CreditNote), ref _creditNote, value);
    }
}
