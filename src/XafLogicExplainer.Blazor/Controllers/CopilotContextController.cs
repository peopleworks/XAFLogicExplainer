using DevExpress.ExpressApp;

namespace XafLogicExplainer.Blazor.Controllers;

/// <summary>
/// ViewController that tracks the current view context for the Copilot widget.
/// Provides view name and object type information that can be used
/// to contextualize Copilot responses.
/// </summary>
public class CopilotContextController : ViewController
{
    /// <summary>
    /// Current view context information. Updated when the user navigates.
    /// </summary>
    public static CopilotContext CurrentContext { get; private set; } = new();

    protected override void OnActivated()
    {
        base.OnActivated();
        UpdateContext();
    }

    protected override void OnViewChanged()
    {
        base.OnViewChanged();
        UpdateContext();
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();
    }

    private void UpdateContext()
    {
        if (View == null) return;

        CurrentContext = new CopilotContext
        {
            ViewId = View.Id,
            ViewCaption = View.Caption,
            ObjectTypeName = View.ObjectTypeInfo?.Type?.Name,
            ObjectTypeFullName = View.ObjectTypeInfo?.Type?.FullName,
            IsListView = View is ListView,
            IsDetailView = View is DetailView,
            NavigationItemId = Frame?.GetController<DevExpress.ExpressApp.SystemModule.ShowNavigationItemController>()
                ?.ShowNavigationItemAction?.SelectedItem?.Id
        };
    }
}

/// <summary>
/// Context information about the current XAF view.
/// </summary>
public class CopilotContext
{
    /// <summary>
    /// XAF view identifier.
    /// </summary>
    public string? ViewId { get; set; }

    /// <summary>
    /// Localized view caption.
    /// </summary>
    public string? ViewCaption { get; set; }

    /// <summary>
    /// Short object type name for the current view.
    /// </summary>
    public string? ObjectTypeName { get; set; }

    /// <summary>
    /// Fully-qualified object type name for the current view.
    /// </summary>
    public string? ObjectTypeFullName { get; set; }

    /// <summary>
    /// Indicates whether the active view is a list view.
    /// </summary>
    public bool IsListView { get; set; }

    /// <summary>
    /// Indicates whether the active view is a detail view.
    /// </summary>
    public bool IsDetailView { get; set; }

    /// <summary>
    /// Navigation item identifier selected in the main navigation controller.
    /// </summary>
    public string? NavigationItemId { get; set; }
}
