using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Staffing.Module.BusinessObjects;

/// <summary>
/// A shift in the scheduler, on the library's <c>Event</c>.
/// </summary>
[DefaultClassOptions]
[NavigationItem("Planning")]
public class Shift : Event
{
    public Shift(Session session) : base(session) { }

    private Employee _supervisor;

    public Employee Supervisor
    {
        get => _supervisor;
        set => SetPropertyValue(nameof(Supervisor), ref _supervisor, value);
    }
}
