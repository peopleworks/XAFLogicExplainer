using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Invoicing.Module.BusinessObjects;

[DefaultClassOptions]
public class Invoice : BaseObject
{
    public Invoice(Session session) : base(session) { }

    private string _number = string.Empty;
    public string Number
    {
        get => _number;
        set => SetPropertyValue(nameof(Number), ref _number, value);
    }

    private Customer? _customer;
    public Customer? Customer
    {
        get => _customer;
        set => SetPropertyValue(nameof(Customer), ref _customer, value);
    }

    private DateTime _date;
    public DateTime Date
    {
        get => _date;
        set => SetPropertyValue(nameof(Date), ref _date, value);
    }

    private decimal _total;
    public decimal Total
    {
        get => _total;
        set => SetPropertyValue(nameof(Total), ref _total, value);
    }

    private bool _isApproved;
    public bool IsApproved
    {
        get => _isApproved;
        set => SetPropertyValue(nameof(IsApproved), ref _isApproved, value);
    }
}
