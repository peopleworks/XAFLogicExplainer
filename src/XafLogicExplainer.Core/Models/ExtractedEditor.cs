namespace XafLogicExplainer.Core.Models;

/// <summary>
/// A property or list editor written by the team rather than shipped by XAF.
/// </summary>
/// <remarks>
/// The same category of hidden behaviour as the Model Editor: a property with a custom editor does
/// not render the way its type says it should, and nothing in the business class says so. An agent
/// that does not know one exists will suggest changes to a control that is not on the screen, and
/// a developer inheriting the application will not find the code until something breaks.
/// <para>
/// They also usually live in the <em>platform</em> project — <c>*.Blazor.Server</c>, <c>*.Win</c> —
/// beside the module rather than in it, which is why nobody reading the business objects meets them.
/// </para>
/// </remarks>
public class ExtractedEditor
{
    /// <summary>Class name, e.g. <c>MapsMarkerPropertyEditor</c>.</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Namespace it lives in.</summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>File it was read from.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Project it lives in, which is usually a platform project beside the module.</summary>
    public string? SourceProject { get; set; }

    /// <summary>What kind of editor it is.</summary>
    public EditorKind Kind { get; set; } = EditorKind.Unknown;

    /// <summary>
    /// The type it renders, from the registration attribute — often an interface, so it applies to
    /// every property whose type implements it.
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Alias a property can request with <c>[EditorAlias]</c>, when the editor declares one.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// True when it replaces the default editor for <see cref="TargetType"/> everywhere, rather
    /// than only where it is asked for by alias.
    /// </summary>
    /// <remarks>
    /// The difference matters: a default editor changes screens nobody edited.
    /// </remarks>
    public bool IsDefault { get; set; }

    /// <summary>Base class, e.g. <c>BlazorPropertyEditorBase</c> or <c>ListEditor</c>.</summary>
    public string BaseType { get; set; } = string.Empty;

    /// <summary>Summary comment, when the class carries one.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Client-side files the editor names — JavaScript or CSS it cannot work without.
    /// </summary>
    /// <remarks>
    /// Behaviour that is neither C# nor XML. It is why a control misbehaves after a file is
    /// renamed, and it is invisible to anyone reading the editor's C# alone.
    /// </remarks>
    public List<string> ClientAssets { get; set; } = [];

    /// <summary>Entities with a property this editor renders, filled in after extraction.</summary>
    public List<string> UsedBy { get; set; } = [];
}

/// <summary>What part of the UI an editor replaces.</summary>
public enum EditorKind
{
    /// <summary>Could not be established.</summary>
    Unknown,

    /// <summary>Renders one property on a detail view.</summary>
    PropertyEditor,

    /// <summary>Renders a whole collection in place of the grid.</summary>
    ListEditor,

    /// <summary>A view item placed on a view through the Model Editor.</summary>
    ViewItem,
}
