using DevExpress.Persistent.Base;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// The same board, narrowed to overtime. It stores nothing either, and says so nowhere itself:
/// only its base does.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Planning")]
public class OvertimeBoard : StaffingBoard
{
    public decimal HoursAbove { get; set; }
}
