using System;
using System.ComponentModel;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;

namespace SampleApp.Module.BusinessObjects;

/// <summary>
/// A row written when something changes. Derived from <c>XPBaseObject</c>, which carries no key
/// of its own, so the class supplies one.
/// </summary>
/// <remarks>
/// <c>XPBaseObject</c> is the ancestor of <c>XPCustomObject</c> and a documented base for
/// persistent classes in its own right — mapping onto a table that already has its own key is
/// exactly when it is the right choice.
/// </remarks>
[DefaultClassOptions]
[NavigationItem("Audit")]
public class AuditEntry : XPBaseObject
{
    public AuditEntry(Session session) : base(session) { }

    [Key(AutoGenerate = true), Browsable(false)]
    public int Oid { get; set; }

    [Size(80)]
    public string ChangedBy { get; set; }

    public DateTime ChangedOn { get; set; }
}
