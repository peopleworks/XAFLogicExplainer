using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Money taken against a sale.</summary>
[DefaultClassOptions]
[XafDefaultProperty(nameof(Reference))]
[Description("Money taken against a sale.")]
public class Payment : XPCustomObject
{
    public Payment(Session session) : base(session) { }

    private string _reference;
    [Size(40)]
    public string Reference { get => _reference; set => SetPropertyValue(nameof(Reference), ref _reference, value); }

    private decimal _amount;
    public decimal Amount { get => _amount; set => SetPropertyValue(nameof(Amount), ref _amount, value); }

    private PaymentMethod _method;
    [Association("Method-Payments")]
    public PaymentMethod Method { get => _method; set => SetPropertyValue(nameof(Method), ref _method, value); }

    private Sale _sale;
    [Association("Sale-Payments")]
    public Sale Sale { get => _sale; set => SetPropertyValue(nameof(Sale), ref _sale, value); }
}
