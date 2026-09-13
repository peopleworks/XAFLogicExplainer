using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Xpo;

namespace Library.Module.Reports.Catalog;

/// <summary>
/// The dialog for printing genre labels, named like the shelf one in the namespace above it.
/// </summary>
[DomainComponent]
public class LabelParameters : ReportParametersObjectBase
{
    public LabelParameters(IObjectSpaceCreator provider) : base(provider) { }

    public string Genre { get; set; }

    public override CriteriaOperator GetCriteria() => CriteriaOperator.Parse("Code Is Not Null");

    public override SortProperty[] GetSorting() => [];
}
