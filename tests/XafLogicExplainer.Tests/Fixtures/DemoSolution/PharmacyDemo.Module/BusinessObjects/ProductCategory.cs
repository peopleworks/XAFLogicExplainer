using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Groups products for browsing and reporting.</summary>
[DefaultClassOptions]
[NavigationItem("Catalogue")]
[XafDefaultProperty(nameof(Name))]
[Description("Groups products for browsing and reporting.")]
public class ProductCategory : XPCustomObject
{
    public ProductCategory(Session session) : base(session) { }

    private string _name;
    [Size(80)]
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    [Association("Category-Products")]
    public XPCollection<Product> Products => GetCollection<Product>(nameof(Products));
}
