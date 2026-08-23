using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Xpo;

namespace Invoicing.Module.Reports;

/// <summary>
/// Written for the overdue report and never wired to it: the registration passes no parameters
/// type. A parameters object nothing registers is still code somebody meant.
/// </summary>
[DomainComponent]
public class OverdueParameters : ReportParametersObjectBase
{
    public OverdueParameters(IObjectSpaceCreator provider) : base(provider) { }

    public int DaysOverdue { get; set; } = 30;

    public override CriteriaOperator GetCriteria() =>
        CriteriaOperator.Parse("IsApproved = True And Date < ?", DateTime.Today.AddDays(-DaysOverdue));

    public override SortProperty[] GetSorting() => [];
}
