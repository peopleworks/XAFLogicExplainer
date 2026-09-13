using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.ReportsV2;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// The dialog shown before the payroll report opens.
/// </summary>
/// <remarks>
/// A <c>[DomainComponent]</c> like <see cref="ShiftSettings"/>, and deliberately not listed with the
/// business classes: the report extraction reads it as the report's parameters, with its criteria,
/// and a dialog listed beside <c>Employee</c> would say the application has a class of that name to
/// store.
/// </remarks>
[DomainComponent]
public class PayrollParameters : ReportParametersObjectBase
{
    public PayrollParameters(IObjectSpaceCreator provider) : base(provider) { }

    public Department Department { get; set; }

    public override CriteriaOperator GetCriteria() =>
        Department is null ? null : CriteriaOperator.Parse("Department.Oid = ?", Department.Oid);

    public override SortProperty[] GetSorting() => [];
}
