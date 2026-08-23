using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.ReportsV2;
using DevExpress.Xpo;
using Invoicing.Module.BusinessObjects;

namespace Invoicing.Module.Reports;

/// <summary>
/// The dialog XAF shows before the customer statement opens. Every property is a parameter;
/// <see cref="GetCriteria"/> is what the parameters mean.
/// </summary>
[DomainComponent]
public class StatementParameters : ReportParametersObjectBase
{
    public StatementParameters(IObjectSpaceCreator provider) : base(provider) { }

    public Customer? Customer { get; set; }

    public DateTime From { get; set; } = DateTime.Today.AddMonths(-1);

    public DateTime To { get; set; } = DateTime.Today;

    public bool ApprovedOnly { get; set; } = true;

    public override CriteriaOperator GetCriteria()
    {
        var criteria = new List<CriteriaOperator>
        {
            CriteriaOperator.Parse("Date >= ? And Date < ?", From.Date, To.Date.AddDays(1)),
        };

        if (Customer is not null)
            criteria.Add(CriteriaOperator.Parse("Customer.Oid = ?", Customer.Oid));

        if (ApprovedOnly)
            criteria.Add(CriteriaOperator.Parse("IsApproved = True"));

        return CriteriaOperator.And(criteria);
    }

    public override SortProperty[] GetSorting() =>
        [new SortProperty("Date", DevExpress.Xpo.DB.SortingDirection.Ascending)];
}
