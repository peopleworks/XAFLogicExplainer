using System;
using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;

namespace PharmacyDemo.Module.BusinessObjects;

/// <summary>A completed transaction at the counter.</summary>
[DefaultClassOptions]
[NavigationItem("Sales")]
[XafDefaultProperty(nameof(Number))]
[Description("A completed transaction at the counter.")]
[Appearance("SaleLockedWhenVoided", Criteria = "IsVoided", TargetItems = "*", Context = "DetailView", Enabled = false)]
[RuleCriteria("Sale_TotalNotNegative", DefaultContexts.Save, "Total >= 0", CustomMessageTemplate = "A sale total cannot be negative.")]
public class Sale : XPCustomObject
{
    public Sale(Session session) : base(session) { }

    private string _number;
    [Size(30)]
    [RuleRequiredField("Sale_Number", DefaultContexts.Save, CustomMessageTemplate = "Every sale needs a number.")]
    public string Number { get => _number; set => SetPropertyValue(nameof(Number), ref _number, value); }

    private DateTime _soldOn;
    public DateTime SoldOn { get => _soldOn; set => SetPropertyValue(nameof(SoldOn), ref _soldOn, value); }

    private bool _isVoided;
    public bool IsVoided { get => _isVoided; set => SetPropertyValue(nameof(IsVoided), ref _isVoided, value); }

    private Patient _patient;
    [Association("Patient-Sales")]
    public Patient Patient { get => _patient; set => SetPropertyValue(nameof(Patient), ref _patient, value); }

    private Pharmacist _pharmacist;
    [Association("Pharmacist-Sales")]
    [DataSourceCriteria("IsOnDuty = True")]
    public Pharmacist Pharmacist { get => _pharmacist; set => SetPropertyValue(nameof(Pharmacist), ref _pharmacist, value); }

    [Association("Sale-Lines"), Aggregated]
    public XPCollection<SaleLine> Lines => GetCollection<SaleLine>(nameof(Lines));

    [Association("Sale-Payments"), Aggregated]
    public XPCollection<Payment> Payments => GetCollection<Payment>(nameof(Payments));

    [PersistentAlias("Lines.Sum(LineTotal)")]
    [Description("Sum of every line on this sale.")]
    public decimal Total => (decimal)EvaluateAlias(nameof(Total));

    [PersistentAlias("Payments.Sum(Amount)")]
    public decimal Paid => (decimal)EvaluateAlias(nameof(Paid));
}
