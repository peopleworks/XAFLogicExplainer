using DevExpress.ExpressApp.DC;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// The dialog behind the Configure Shifts action. No base class at all: <c>[DomainComponent]</c> is
/// the only thing that puts it in the application model.
/// </summary>
[DomainComponent]
public class ShiftSettings
{
    public int WeeklyHours { get; set; }

    public bool AllowOvernight { get; set; }
}
