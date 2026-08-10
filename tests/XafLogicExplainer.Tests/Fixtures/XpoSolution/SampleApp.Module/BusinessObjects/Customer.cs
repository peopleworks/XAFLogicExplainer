using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleApp.Module.BusinessObjects;

/// <summary>
/// A company that places orders.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDefaultProperty(nameof(Name))]
[Description("A company that places orders.")]
public class Customer : XPCustomObject
{
    public Customer(Session session) : base(session) { }

    private string _name;

    [Size(120)]
    [RuleRequiredField("Customer_Name_Required", DefaultContexts.Save,
        CustomMessageTemplate = "A customer must have a name.")]
    [Description("Registered trading name.")]
    public string Name
    {
        get => _name;
        set => SetPropertyValue(nameof(Name), ref _name, value);
    }

    private string _taxId;

    [Size(20)]
    [Description("National tax identifier.")]
    public string TaxId
    {
        get => _taxId;
        set => SetPropertyValue(nameof(TaxId), ref _taxId, value);
    }

    private bool _isBlocked;

    [Description("Blocked customers cannot receive new orders.")]
    public bool IsBlocked
    {
        get => _isBlocked;
        set => SetPropertyValue(nameof(IsBlocked), ref _isBlocked, value);
    }

    [Association("Customer-Orders")]
    public XPCollection<Order> Orders => GetCollection<Order>(nameof(Orders));
}
