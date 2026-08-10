using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>One product on a prescription.</summary>
[DefaultClassOptions]
[XafDefaultProperty(nameof(Quantity))]
[Description("One product on a prescription.")]
public class PrescriptionLine : XPCustomObject
{
    public PrescriptionLine(Session session) : base(session) { }

    private Product _product;
    [DataSourceCriteria("RequiresPrescription = True")]
    public Product Product { get => _product; set => SetPropertyValue(nameof(Product), ref _product, value); }

    private int _quantity;
    public int Quantity { get => _quantity; set => SetPropertyValue(nameof(Quantity), ref _quantity, value); }

    private Prescription _prescription;
    [Association("Prescription-Lines")]
    public Prescription Prescription { get => _prescription; set => SetPropertyValue(nameof(Prescription), ref _prescription, value); }
}
