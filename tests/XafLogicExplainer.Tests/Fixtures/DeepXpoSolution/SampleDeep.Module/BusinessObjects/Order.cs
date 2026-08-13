using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace SampleDeep.Module.BusinessObjects;

/// <summary>Two hops from a persistent root: Order -> NamedBaseObject -> BaseObject.</summary>
[DefaultClassOptions]
[NavigationItem("Sales")]
public class Order : NamedBaseObject
{
    public Order(Session session) : base(session) { }

    [RuleRequiredField("Order_Number_Required", DefaultContexts.Save)]
    public string Number
    {
        get => GetPropertyValue<string>(nameof(Number));
        set => SetPropertyValue(nameof(Number), value);
    }
}
