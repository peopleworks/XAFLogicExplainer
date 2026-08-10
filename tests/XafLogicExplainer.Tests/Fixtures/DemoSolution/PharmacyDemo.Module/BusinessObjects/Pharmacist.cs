using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Staff licensed to dispense.</summary>
[DefaultClassOptions]
[NavigationItem("Staff")]
[XafDefaultProperty(nameof(FullName))]
[Description("Staff licensed to dispense.")]
public class Pharmacist : XPCustomObject
{
    public Pharmacist(Session session) : base(session) { }

    [Size(140)]
    private string _fullName;
    public string FullName { get => _fullName; set => SetPropertyValue(nameof(FullName), ref _fullName, value); }

    [Size(40)]
    private string _licenceNumber;
    public string LicenceNumber { get => _licenceNumber; set => SetPropertyValue(nameof(LicenceNumber), ref _licenceNumber, value); }

    private bool _isOnDuty;
    public bool IsOnDuty { get => _isOnDuty; set => SetPropertyValue(nameof(IsOnDuty), ref _isOnDuty, value); }

    [Association("Pharmacist-Sales")]
    public XPCollection<Sale> Sales => GetCollection<Sale>(nameof(Sales));
}
