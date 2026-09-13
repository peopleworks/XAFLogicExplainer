using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A planning screen that stores nothing: the employees still free this week, gathered in memory.
/// </summary>
/// <remarks>
/// XAF generates a list, detail and lookup view for it like any other class, and a nested list
/// view for its collection — which the Model Editor file beside this module customizes.
/// </remarks>
[DefaultClassOptions, DomainComponent]
[NavigationItem("Planning")]
public class StaffingBoard : NonPersistentBaseObject
{
    public DateTime WeekStarting { get; set; }

    public IList<Employee> Available { get; } = new List<Employee>();
}
