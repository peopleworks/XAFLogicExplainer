using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>A person the pharmacy dispenses to.</summary>
[DefaultClassOptions]
[NavigationItem("Care")]
[XafDefaultProperty(nameof(FullName))]
[Description("A person the pharmacy dispenses to.")]
public class Patient : XPCustomObject
{
    public Patient(Session session) : base(session) { }

    private string _fullName;
    [Size(140)]
    [RuleRequiredField("Patient_Name", DefaultContexts.Save, CustomMessageTemplate = "A patient must have a name.")]
    public string FullName { get => _fullName; set => SetPropertyValue(nameof(FullName), ref _fullName, value); }

    private DateTime _dateOfBirth;
    public DateTime DateOfBirth { get => _dateOfBirth; set => SetPropertyValue(nameof(DateOfBirth), ref _dateOfBirth, value); }

    private string _allergies;
    [Size(400)]
    [Description("Checked before dispensing.")]
    public string Allergies { get => _allergies; set => SetPropertyValue(nameof(Allergies), ref _allergies, value); }

    private InsurancePlan _plan;
    [Association("Plan-Patients")]
    public InsurancePlan Plan { get => _plan; set => SetPropertyValue(nameof(Plan), ref _plan, value); }

    [Association("Patient-Prescriptions")]
    public XPCollection<Prescription> Prescriptions => GetCollection<Prescription>(nameof(Prescriptions));

    [Association("Patient-Sales")]
    public XPCollection<Sale> Sales => GetCollection<Sale>(nameof(Sales));
}
