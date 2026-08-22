using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace Billing.Module.BusinessObjects;

/// <summary>
/// Money given back against an invoice.
/// </summary>
[DefaultClassOptions]
[Description("Money given back against an invoice.")]
public class CreditNote : XPCustomObject
{
    public CreditNote(Session session) : base(session) { }

    private decimal _amount;

    public decimal Amount
    {
        get => _amount;
        set => SetPropertyValue(nameof(Amount), ref _amount, value);
    }
}
