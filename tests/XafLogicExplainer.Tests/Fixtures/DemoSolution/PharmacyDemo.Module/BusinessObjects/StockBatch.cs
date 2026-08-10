using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>A delivered lot with its own expiry date.</summary>
[DefaultClassOptions]
[XafDefaultProperty(nameof(LotNumber))]
[Description("A delivered lot with its own expiry date.")]
[Appearance("BatchExpired", Criteria = "ExpiresOn < LocalDateTimeToday()", TargetItems = "*", BackColor = "LightSalmon")]
public class StockBatch : XPCustomObject
{
    public StockBatch(Session session) : base(session) { }

    [Size(40)]
    private string _lotNumber;
    public string LotNumber { get => _lotNumber; set => SetPropertyValue(nameof(LotNumber), ref _lotNumber, value); }

    private DateTime _expiresOn;
    public DateTime ExpiresOn { get => _expiresOn; set => SetPropertyValue(nameof(ExpiresOn), ref _expiresOn, value); }

    private int _remainingUnits;
    public int RemainingUnits { get => _remainingUnits; set => SetPropertyValue(nameof(RemainingUnits), ref _remainingUnits, value); }

    [Association("Product-Batches")]
    private Product _product;
    public Product Product { get => _product; set => SetPropertyValue(nameof(Product), ref _product, value); }
}
