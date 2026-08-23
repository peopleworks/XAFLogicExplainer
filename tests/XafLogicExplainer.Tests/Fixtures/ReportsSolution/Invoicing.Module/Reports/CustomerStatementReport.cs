using System.Reflection;
using DevExpress.XtraReports.UI;

namespace Invoicing.Module.Reports;

/// <summary>
/// Layout shipped as an embedded <c>.repx</c>, the way a report designed outside Visual Studio
/// arrives in a module.
/// </summary>
public class CustomerStatementReport : XtraReport
{
    public CustomerStatementReport()
    {
        using var layout = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Invoicing.Module.Reports.CustomerStatement.repx");
        LoadLayoutFromXml(layout!);
    }
}
