using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>Authority to dispense a restricted product.</summary>
[DefaultClassOptions]
[NavigationItem("Care")]
[XafDefaultProperty(nameof(Reference))]
[Description("Authority to dispense a restricted product.")]
[RuleCriteria("Prescription_NotExpired", DefaultContexts.Save, "ExpiresOn > IssuedOn", CustomMessageTemplate = "A prescription cannot expire before it was issued.")]
public class Prescription : XPCustomObject
{
    public Prescription(Session session) : base(session) { }

    [Size(30)]
    private string _reference;
    public string Reference { get => _reference; set => SetPropertyValue(nameof(Reference), ref _reference, value); }

    private DateTime _issuedOn;
    public DateTime IssuedOn { get => _issuedOn; set => SetPropertyValue(nameof(IssuedOn), ref _issuedOn, value); }

    private DateTime _expiresOn;
    public DateTime ExpiresOn { get => _expiresOn; set => SetPropertyValue(nameof(ExpiresOn), ref _expiresOn, value); }

    private bool _isDispensed;
    public bool IsDispensed { get => _isDispensed; set => SetPropertyValue(nameof(IsDispensed), ref _isDispensed, value); }

    [Association("Patient-Prescriptions")]
    private Patient _patient;
    public Patient Patient { get => _patient; set => SetPropertyValue(nameof(Patient), ref _patient, value); }

    [Association("Prescriber-Prescriptions")]
    private Prescriber _prescriber;
    public Prescriber Prescriber { get => _prescriber; set => SetPropertyValue(nameof(Prescriber), ref _prescriber, value); }

    [Association("Prescription-Lines"), Aggregated]
    public XPCollection<PrescriptionLine> Lines => GetCollection<PrescriptionLine>(nameof(Lines));
}
