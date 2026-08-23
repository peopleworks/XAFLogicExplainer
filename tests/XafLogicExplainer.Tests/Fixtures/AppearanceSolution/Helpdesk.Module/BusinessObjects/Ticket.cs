using System.ComponentModel;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;

namespace Helpdesk.Module.BusinessObjects;

/// <summary>
/// A support ticket, whose appearance rules are written every way the attribute allows.
/// </summary>
/// <remarks>
/// Its proportions are the point. Every rule here passes its criteria by position, and one names an
/// Action rather than a property — the two forms no other fixture uses, which is exactly why the
/// suite agreed that both were extracted correctly.
/// </remarks>
[DefaultClassOptions]
[Description("A support ticket.")]
[Appearance("Ticket_ClosedIsGrey", "Status = 'Closed'", FontColor = "Gray")]
[Appearance("Ticket_LockActions", AppearanceItemType.Action, "Status = 'Closed'", TargetItems = "Delete")]
[Appearance("Ticket_UrgentLayout", AppearanceItemType = "LayoutItem", TargetItems = "EscalationGroup",
    Criteria = "Priority = 'Urgent'", BackColor = "Red")]
public class Ticket : BaseObject
{
    public Ticket(Session session) : base(session) { }

    private string _status;

    public string Status
    {
        get => _status;
        set => SetPropertyValue(nameof(Status), ref _status, value);
    }

    private string _priority;

    public string Priority
    {
        get => _priority;
        set => SetPropertyValue(nameof(Priority), ref _priority, value);
    }

    private string _resolution;

    [Appearance("Ticket_ResolutionWhenClosed", "Status <> 'Closed'", Enabled = false)]
    public string Resolution
    {
        get => _resolution;
        set => SetPropertyValue(nameof(Resolution), ref _resolution, value);
    }
}
