using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;
using SampleApp.Module.BusinessObjects;

namespace SampleApp.Module.Controllers;

/// <summary>
/// The shape real XAF code takes, and the shape extraction used to lose.
/// </summary>
/// <remarks>
/// Three things at once, all of them ordinary: a base class that holds the targeting for the
/// controllers below it, several small controllers sharing one file, and a controller that extends
/// something DevExpress ships instead of extending <c>ViewController</c> directly.
/// </remarks>
public abstract class OrderListControllerBase : ViewController
{
    protected OrderListControllerBase()
    {
        TargetObjectType = typeof(Order);
        TargetViewType = ViewType.ListView;
    }
}

/// <summary>Approves several orders at once. Targets nothing itself.</summary>
public class BulkApproveController : OrderListControllerBase
{
}

/// <summary>Exports the current selection. Also targets nothing itself.</summary>
public class OrderExportController : OrderListControllerBase
{
}

/// <summary>
/// Changes how deletion works application-wide, by extending the controller that provides it.
/// </summary>
public class ArchiveInsteadOfDeleteController : DeleteObjectsViewController
{
}
