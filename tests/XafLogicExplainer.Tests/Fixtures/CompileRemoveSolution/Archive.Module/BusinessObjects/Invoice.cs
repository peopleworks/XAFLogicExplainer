using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Archive.Module.BusinessObjects;

/// <summary>
/// The invoice the application compiles. A copy of it under <c>Catalog</c> is removed from the build.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Billing")]
public class Invoice : BaseObject
{
    public Invoice(Session session) : base(session) { }

    private string _number;

    public string Number
    {
        get => _number;
        set => SetPropertyValue(nameof(Number), ref _number, value);
    }

    private Customer _customer;

    [Association("Customer-Invoices")]
    public Customer Customer
    {
        get => _customer;
        set => SetPropertyValue(nameof(Customer), ref _customer, value);
    }
}
