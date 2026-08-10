using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Cash, card, or an insurer.</summary>
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDefaultProperty(nameof(Name))]
[Description("Cash, card, or an insurer.")]
public class PaymentMethod : XPCustomObject
{
    public PaymentMethod(Session session) : base(session) { }

    [Size(60)]
    private string _name;
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    private bool _requiresAuthorization;
    public bool RequiresAuthorization { get => _requiresAuthorization; set => SetPropertyValue(nameof(RequiresAuthorization), ref _requiresAuthorization, value); }

    [Association("Method-Payments")]
    public XPCollection<Payment> Payments => GetCollection<Payment>(nameof(Payments));
}
