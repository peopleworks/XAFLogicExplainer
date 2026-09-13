using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// The application's own dashboard storage class, set as the Dashboards module's
/// <c>DashboardDataType</c>.
/// </summary>
[MapInheritance(MapInheritanceType.ParentTable)]
public class StaffingDashboard : DashboardData
{
    public StaffingDashboard(Session session) : base(session) { }

    private string _area;

    public string Area
    {
        get => _area;
        set => SetPropertyValue(nameof(Area), ref _area, value);
    }
}
