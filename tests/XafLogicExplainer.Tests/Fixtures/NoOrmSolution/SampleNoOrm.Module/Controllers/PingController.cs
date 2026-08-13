using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;

namespace SampleNoOrm.Module.Controllers;

/// <summary>
/// A module that persists nothing: no entity, no DbContext, no XPO type.
/// </summary>
/// <remarks>
/// A UI-only or utility module is a real thing to point the extractor at — and there is no
/// evidence in it for either ORM. That is a fact about the project, not a reason to pick one.
/// </remarks>
public class PingController : ViewController
{
    public PingController()
    {
        var ping = new SimpleAction(this, "Ping", "Tools");
        ping.Execute += (_, _) => Application.ShowViewStrategy.ShowMessage("pong");
    }
}
