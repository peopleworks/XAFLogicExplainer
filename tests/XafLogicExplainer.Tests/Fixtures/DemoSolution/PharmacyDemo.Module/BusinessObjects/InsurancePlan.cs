using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Who pays, and how much of it.</summary>
[DefaultClassOptions]
[NavigationItem("Care")]
[XafDefaultProperty(nameof(Name))]
[Description("Who pays, and how much of it.")]
public class InsurancePlan : XPCustomObject
{
    public InsurancePlan(Session session) : base(session) { }

    private string _name;
    [Size(120)]
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    private decimal _coveragePercent;
    [Description("Share of the price the insurer pays.")]
    public decimal CoveragePercent { get => _coveragePercent; set => SetPropertyValue(nameof(CoveragePercent), ref _coveragePercent, value); }

    [Association("Plan-Patients")]
    public XPCollection<Patient> Patients => GetCollection<Patient>(nameof(Patients));
}
