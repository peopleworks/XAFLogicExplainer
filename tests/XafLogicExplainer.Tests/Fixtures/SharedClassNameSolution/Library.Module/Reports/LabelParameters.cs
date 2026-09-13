using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Xpo;

namespace Library.Module.Reports;

/// <summary>
/// The dialog for printing shelf labels. A second dialog in the catalog has the same name.
/// </summary>
[DomainComponent]
public class LabelParameters : ReportParametersObjectBase
{
    public LabelParameters(IObjectSpaceCreator provider) : base(provider) { }

    public string Shelf { get; set; }

    public override CriteriaOperator GetCriteria() => CriteriaOperator.Parse("Title Is Not Null");

    public override SortProperty[] GetSorting() => [];
}
