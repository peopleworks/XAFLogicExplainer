using System.Text.Json.Serialization;

namespace XafLogicExplainer.Core.Models;

/// <summary>
/// The conditions XAF checks before it activates a controller on a view.
/// </summary>
/// <remarks>
/// This is not an interpretation. <c>ViewController.IsFitToView</c> ands four independent tests
/// together — nesting, view type, object type and view id — and a controller runs on a view when
/// all four pass. Every one of them is unrestricted when left unset, so a controller that sets none
/// of them activates on <em>every</em> view in the application.
/// <para>
/// The object type test is assignability, not equality: a controller targeting a base class also
/// runs on the views of every class derived from it. Reading it as equality would under-report the
/// logic that touches a screen, which is the failure this project exists to prevent.
/// </para>
/// </remarks>
public sealed class ControllerTargeting
{
    /// <summary>
    /// Business class the view's objects must be assignable to, or null for any type.
    /// </summary>
    public string? TargetObjectType { get; set; }

    /// <summary>
    /// View class the view must be — <c>ListView</c>, <c>DetailView</c>, <c>DashboardView</c>,
    /// <c>ObjectView</c> — or null for any.
    /// </summary>
    /// <remarks>
    /// XAF exposes this twice: as <c>TypeOfView</c> and as the <c>TargetViewType</c> enum, whose
    /// setter writes the same field. Both spellings are normalized here to the view class name.
    /// </remarks>
    public string? TypeOfView { get; set; }

    /// <summary>
    /// Nesting the view must have, <c>Root</c> or <c>Nested</c>; null when either will do.
    /// </summary>
    public string? Nesting { get; set; }

    /// <summary>
    /// View identifiers this controller is restricted to. Empty means any view.
    /// </summary>
    /// <remarks>
    /// XAF accepts a semicolon-separated list in the single <c>TargetViewId</c> string, which is
    /// split here so a reader does not have to know that.
    /// </remarks>
    public List<string> ViewIds { get; set; } = [];

    /// <summary>
    /// The expression assigned to <c>TargetViewId</c> when it was not a literal this analysis could
    /// resolve — a constant belonging to another type, or a value built at run time.
    /// </summary>
    /// <remarks>
    /// Kept rather than dropped. A controller restricted to a view id we cannot name is still
    /// restricted, and treating it as unrestricted would state something false about the
    /// application. Naming the expression lets a reader finish the job.
    /// </remarks>
    public string? UnresolvedViewId { get; set; }

    /// <summary>
    /// The base controller whose own targeting could not be established, when there is one.
    /// </summary>
    /// <remarks>
    /// Targeting is inherited, so a controller extending a class nobody can resolve has
    /// <em>unknown</em> targeting, not none. Without that distinction a controller extending
    /// <c>DeleteObjectsViewController</c> on a machine with no catalog reads as "restricts
    /// nothing, runs on every screen" — a confident claim built out of an absence of information.
    /// </remarks>
    public string? UnresolvedBase { get; set; }

    /// <summary>
    /// Assignments to one of the four conditions whose value this analysis could not read.
    /// </summary>
    /// <remarks>
    /// Seen and not understood is a third state, and it used to collapse into "unrestricted".
    /// <c>TargetViewType = isList ? ViewType.ListView : ViewType.DetailView</c> was read as a
    /// confident restriction to <c>DetailView</c> — the last word in the expression — and
    /// <c>TargetObjectType = FindTypeInfo(name).Type</c> as no restriction at all. Both are
    /// definite statements about an application, built out of not having understood a line.
    /// </remarks>
    public List<string> Unreadable { get; set; } = [];

    /// <summary>
    /// Which of these conditions the controller set itself, rather than inheriting.
    /// </summary>
    /// <remarks>
    /// Needed to layer a controller over its base correctly, because null is ambiguous: a
    /// controller that never mentions <c>TargetViewType</c> and one that sets it to
    /// <c>ViewType.Any</c> both end up null here, and only the second means "ignore what my base
    /// said". Constructors run base-first, so a declared value wins.
    /// <para>
    /// Not serialized. It describes how this object was read, not the application, and by the time
    /// a catalog is written the layering has already been done.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public HashSet<string> Declared { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether nothing constrains this controller, so it activates on every view in the application.
    /// </summary>
    [JsonIgnore]
    public bool IsUnrestricted =>
        TargetObjectType is null
        && TypeOfView is null
        && Nesting is null
        && ViewIds.Count == 0
        && UnresolvedViewId is null
        && UnresolvedBase is null
        && Unreadable.Count == 0;

    /// <summary>
    /// Whether something about this controller's activation could not be read from the source.
    /// </summary>
    [JsonIgnore]
    public bool IsUndetermined =>
        UnresolvedViewId is not null || UnresolvedBase is not null || Unreadable.Count > 0;
}
