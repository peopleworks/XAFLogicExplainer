using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>A laboratory or distributor the pharmacy buys from.</summary>
[DefaultClassOptions]
[NavigationItem("Purchasing")]
[XafDefaultProperty(nameof(Name))]
[Description("A laboratory or distributor the pharmacy buys from.")]
public class Supplier : XPCustomObject
{
    public Supplier(Session session) : base(session) { }

    private string _name;
    [Size(140)]
    [RuleRequiredField("Supplier_Name", DefaultContexts.Save, CustomMessageTemplate = "A supplier must have a name.")]
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    private string _taxId;
    [Size(20)]
    public string TaxId { get => _taxId; set => SetPropertyValue(nameof(TaxId), ref _taxId, value); }

    private bool _isPreferred;
    [Description("Preferred suppliers are offered first when restocking.")]
    public bool IsPreferred { get => _isPreferred; set => SetPropertyValue(nameof(IsPreferred), ref _isPreferred, value); }

    [Association("Supplier-Products")]
    public XPCollection<Product> Products => GetCollection<Product>(nameof(Products));
}
