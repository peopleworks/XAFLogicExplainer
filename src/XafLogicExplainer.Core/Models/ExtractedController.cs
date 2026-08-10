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
    /// Target business object type when declared.
    /// </summary>
    public string? TargetObjectType { get; set; }

    /// <summary>
    /// Target view type when declared.
    /// </summary>
    public string? TargetViewType { get; set; }

    /// <summary>
    /// Base controller type text.
    /// </summary>
    public string BaseControllerType { get; set; } = string.Empty;

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
