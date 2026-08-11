namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Describes one extracted XAF controller and its action/method metadata.
/// </summary>
public class ExtractedController
{
    /// <summary>
    /// Controller class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Declared namespace.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Source file path where the controller was found.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the project this controller was extracted from (e.g. "Module", "Blazor.Server").
    /// </summary>
    public string? SourceProject { get; set; }

    /// <summary>
    /// Where XAF activates this controller.
    /// </summary>
    public ControllerTargeting Targeting { get; set; } = new();

    /// <summary>
    /// Target business object type when declared.
    /// </summary>
    /// <remarks>One of the four conditions in <see cref="Targeting"/>, kept under its XAF name.</remarks>
    public string? TargetObjectType
    {
        get => Targeting.TargetObjectType;
        set => Targeting.TargetObjectType = value;
    }

    /// <summary>
    /// Target view type when declared.
    /// </summary>
    /// <remarks>
    /// The <c>TargetViewType</c> enum and <c>TypeOfView</c> are the same field in XAF — the enum's
    /// setter writes the type — so this reads <see cref="ControllerTargeting.TypeOfView"/>.
    /// </remarks>
    public string? TargetViewType
    {
        get => Targeting.TypeOfView;
        set => Targeting.TypeOfView = value;
    }

    /// <summary>
    /// Base controller type text.
    /// </summary>
    public string BaseControllerType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the class is abstract, and therefore never activates on anything itself.
    /// </summary>
    /// <remarks>
    /// XAF registers only controllers it can instantiate. An abstract base class exists to hand its
    /// targeting and its actions down to the classes that do run, and reporting it on a screen
    /// invents a controller the application never loads.
    /// </remarks>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Whether this is a window controller, which belongs to a window rather than to a view.
    /// </summary>
    /// <remarks>
    /// It has none of the four view conditions, so its targeting reads as unrestricted — and it
    /// was landing on every screen in the application with "restricts nothing, runs everywhere".
    /// </remarks>
    public bool IsWindowController { get; set; }

    /// <summary>
    /// Actions discovered from fields and constructor wiring.
    /// </summary>
    public List<ExtractedAction> Actions { get; set; } = [];

    /// <summary>
    /// Entity types referenced by casts, type checks, or expressions.
    /// </summary>
    public List<string> ReferencedEntities { get; set; } = [];

    /// <summary>
    /// Optional high-level business summary (reserved for future enrichment).
    /// </summary>
    public string? BusinessLogicSummary { get; set; }

    /// <summary>
    /// Methods extracted from the controller class.
    /// </summary>
    public List<ExtractedMethod> Methods { get; set; } = [];

    /// <summary>
    /// The XAF framework controller this one derives from, when the ground-truth catalog
    /// identified one. Null means the base type is defined by this application, or that no
    /// catalog was available.
    /// </summary>
    public string? FrameworkBaseType { get; set; }

    /// <summary>Official description of <see cref="FrameworkBaseType"/>.</summary>
    public string? FrameworkBaseSummary { get; set; }

    /// <summary>Official documentation page for <see cref="FrameworkBaseType"/>.</summary>
    public string? FrameworkBaseDocumentationUrl { get; set; }

    /// <summary>
    /// Built-in editors this controller reconfigures through
    /// <c>View.CustomizeViewItemControl&lt;T&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// The other way a screen stops behaving the way its business class implies, and the one
    /// DevExpress recommends for small changes. There is no custom editor class to find: a
    /// controller reaches into a built-in editor's component model at run time, so nothing in the
    /// entity, the editor list or the Model Editor mentions it.
    /// </remarks>
    public List<string> CustomizedEditors { get; set; } = [];

    /// <summary>
    /// Source comments associated with the controller declaration.
    /// </summary>
    public List<string> SourceComments { get; set; } = [];
}

/// <summary>
/// Describes one controller action and its runtime configuration.
/// </summary>
public class ExtractedAction
{
    /// <summary>
    /// Stable action identifier.
    /// </summary>
    public string ActionId { get; set; } = string.Empty;

    /// <summary>
    /// Action class/type (SimpleAction, PopupWindowShowAction, and so on).
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable action caption.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Action category used by XAF UI placement.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional confirmation prompt shown before execution.
    /// </summary>
    public string? ConfirmationMessage { get; set; }

    /// <summary>
    /// Associated icon/image name.
    /// </summary>
    public string? ImageName { get; set; }

    /// <summary>
    /// Tooltip text.
    /// </summary>
    public string? ToolTip { get; set; }

    /// <summary>
    /// Execute event handler method name when detected.
    /// </summary>
    public string? ExecuteMethodName { get; set; }

    /// <summary>
    /// Captured execute handler body when method body extraction is enabled.
    /// </summary>
    public string? ExecuteMethodBody { get; set; }

    /// <summary>
    /// Enablement criteria expression when available.
    /// </summary>
    public string? EnabledCriteria { get; set; }

    /// <summary>
    /// Where this action appears, within the views its controller is already active on.
    /// </summary>
    /// <remarks>
    /// An action carries its own copy of the controller's four conditions, and XAF evaluates them
    /// with the same <c>IsFitToView</c>. It can only narrow: an action is never shown on a view
    /// where its controller is inactive.
    /// </remarks>
    public ControllerTargeting Targeting { get; set; } = new();

    /// <summary>
    /// AI-generated plain-language explanation of what this action does in business terms.
    /// </summary>
    public string? BusinessLogicSummary { get; set; }
}

/// <summary>
/// Describes one extracted controller method.
/// </summary>
public class ExtractedMethod
{
    /// <summary>
    /// Method name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Return type name.
    /// </summary>
    public string ReturnType { get; set; } = string.Empty;

    /// <summary>
    /// Parameter signature fragments (type and name).
    /// </summary>
    public List<string> Parameters { get; set; } = [];

    /// <summary>
    /// Method body text or expression body.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether method is public.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// XML summary text from leading documentation trivia when present.
    /// </summary>
    public string? Summary { get; set; }
}
