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

    [Size(120)]
    private string _name;
    public string Name { get => _name; set => SetPropertyValue(nameof(Name), ref _name, value); }

    [Description("Share of the price the insurer pays.")]
    private decimal _coveragePercent;
    public decimal CoveragePercent { get => _coveragePercent; set => SetPropertyValue(nameof(CoveragePercent), ref _coveragePercent, value); }

    [Association("Plan-Patients")]
    public XPCollection<Patient> Patients => GetCollection<Patient>(nameof(Patients));
}
