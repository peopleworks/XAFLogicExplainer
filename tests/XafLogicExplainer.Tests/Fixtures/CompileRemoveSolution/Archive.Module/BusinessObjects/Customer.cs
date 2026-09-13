using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Archive.Module.BusinessObjects;

/// <summary>
/// A customer. <c>Customer.Backup.cs</c> beside it is removed by a pattern.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Billing")]
public class Customer : BaseObject
{
    public Customer(Session session) : base(session) { }

    private string _name;

    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }

    [Association("Customer-Invoices")]
    public XPCollection<Invoice> Invoices => GetCollection<Invoice>(nameof(Invoices));
}
