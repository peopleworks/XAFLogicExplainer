using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>A doctor who writes prescriptions.</summary>
[DefaultClassOptions]
[NavigationItem("Care")]
[XafDefaultProperty(nameof(FullName))]
[Description("A doctor who writes prescriptions.")]
public class Prescriber : XPCustomObject
{
    public Prescriber(Session session) : base(session) { }

    private string _fullName;
    [Size(140)]
    public string FullName { get => _fullName; set => SetPropertyValue(nameof(FullName), ref _fullName, value); }

    private string _licenceNumber;
    [Size(40)]
    public string LicenceNumber { get => _licenceNumber; set => SetPropertyValue(nameof(LicenceNumber), ref _licenceNumber, value); }

    [Association("Prescriber-Prescriptions")]
    public XPCollection<Prescription> Prescriptions => GetCollection<Prescription>(nameof(Prescriptions));
}
