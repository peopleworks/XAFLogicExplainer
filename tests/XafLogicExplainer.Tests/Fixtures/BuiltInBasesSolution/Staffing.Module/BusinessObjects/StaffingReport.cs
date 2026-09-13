using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// The application's own report storage class, set as the Reports V2 module's <c>ReportDataType</c>.
/// </summary>
/// <remarks>
/// Every report a user opens from the Reports navigation item is a row of this class, and its list
/// view is the Reports screen.
/// </remarks>
[MapInheritance(MapInheritanceType.ParentTable)]
public class StaffingReport : ReportDataV2
{
    public StaffingReport(Session session) : base(session) { }

    private string _area;

    public string Area
    {
        get => _area;
        set => SetPropertyValue(nameof(Area), ref _area, value);
    }
}
