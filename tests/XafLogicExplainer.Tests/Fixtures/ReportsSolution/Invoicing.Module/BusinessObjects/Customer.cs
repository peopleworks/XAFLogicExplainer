using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Invoicing.Module.BusinessObjects;

[DefaultClassOptions]
public class Customer : BaseObject
{
    public Customer(Session session) : base(session) { }

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }

    private string _region = string.Empty;
    public string Region
    {
        get => _region;
        set => SetPropertyValue(nameof(Region), ref _region, value);
    }
}
