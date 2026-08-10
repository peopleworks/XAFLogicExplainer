using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>One product line on a sale.</summary>
[DefaultClassOptions]
[XafDefaultProperty(nameof(Quantity))]
[Description("One product line on a sale.")]
public class SaleLine : XPCustomObject
{
    public SaleLine(Session session) : base(session) { }

    private Product _product;
    public Product Product { get => _product; set => SetPropertyValue(nameof(Product), ref _product, value); }

    private int _quantity;
    public int Quantity { get => _quantity; set => SetPropertyValue(nameof(Quantity), ref _quantity, value); }

    private decimal _unitPrice;
    public decimal UnitPrice { get => _unitPrice; set => SetPropertyValue(nameof(UnitPrice), ref _unitPrice, value); }

    private Sale _sale;
    [Association("Sale-Lines")]
    public Sale Sale { get => _sale; set => SetPropertyValue(nameof(Sale), ref _sale, value); }

    [PersistentAlias("Quantity * UnitPrice")]
    public decimal LineTotal => (decimal)EvaluateAlias(nameof(LineTotal));
}
