using DevExpress.Persistent.Base.ReportsV2;
using DevExpress.XtraReports.UI;
using Invoicing.Module.BusinessObjects;

namespace Invoicing.Module.Reports;

/// <summary>
/// Built in code, no designer and no <c>.repx</c>: the shape a quick in-place report takes.
/// </summary>
public class OverdueInvoicesReport : XtraReport
{
    public OverdueInvoicesReport()
    {
        var source = new CollectionDataSource { ObjectTypeName = typeof(Invoice).FullName };
        DataSource = source;
        FilterString = "[IsApproved] = True And [Date] < AddDays(Today(), -30)";

        var detail = new DetailBand();
        var number = new XRLabel();
        number.ExpressionBindings.Add(new ExpressionBinding("Text", "[Number]"));
        detail.Controls.Add(number);
        Bands.Add(detail);
    }
}
