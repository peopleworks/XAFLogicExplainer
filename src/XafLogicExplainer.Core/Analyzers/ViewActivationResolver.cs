using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Works out which controllers run on which view.
/// </summary>
/// <remarks>
/// A transcription of <c>ViewController.IsFitToView</c>, which ands four conditions together:
/// nesting, view type, object type and view id. Nothing here is inferred — each condition is
/// evaluated the way the framework evaluates it, and the reason is recorded so a reader can check
/// the answer instead of trusting it.
/// <para>
/// One condition cannot be answered from source at all. <c>Active["reason"] = …</c> is set at run
/// time, from data, and a controller that passes all four tests can still switch itself off. So
/// this says <em>which controllers XAF loads onto a view</em>, which is a real and useful answer,
/// and not <em>which ones will do something</em>, which no static analysis can give.
/// </para>
/// </remarks>
public static class ViewActivationResolver
{
    /// <summary>
    /// Fills in, for every view, the controllers XAF activates on it.
    /// </summary>
    /// <param name="views">The view inventory, updated in place.</param>
    /// <param name="controllers">The application's controllers.</param>
    /// <param name="entities">Business classes, for the assignability test.</param>
    public static void Resolve(
        IReadOnlyList<ExtractedView> views,
        IReadOnlyList<ExtractedController> controllers,
        IReadOnlyList<ExtractedEntity> entities)
    {
        var ancestry = BuildAncestry(entities);

        foreach (var view in views)
        {
            view.Activates.Clear();

            foreach (var controller in controllers)
            {
                // XAF registers only controllers it can create, so an abstract base class runs on
                // nothing itself. It is still resolved, because the classes below it inherit its
                // targeting.
                if (controller.IsAbstract)
                    continue;

                if (Fits(controller.Targeting, view, ancestry) is not { } reasons)
                    continue;

                view.Activates.Add(new ViewActivation
                {
                    Controller = controller.ClassName,
                    SourceProject = controller.SourceProject,
                    Reasons = reasons,
                    Actions =
                    [
                        .. controller.Actions
                            .Where(action => Fits(action.Targeting, view, ancestry) is not null)
                            .Select(action => action.Caption is { Length: > 0 } caption
                                ? $"{action.ActionId} — {caption}"
                                : action.ActionId),
                    ],
                });
            }
        }
    }

    /// <summary>
    /// Adds the framework's own controllers to each view.
    /// </summary>
    /// <remarks>
    /// The other half of the picture, and the half no repository contains. It is deliberately not
    /// symmetric with an application's own controllers:
    /// <list type="bullet">
    ///   <item>Only the modules this application registers contribute, so a WinForms controller
    ///   never appears on a Blazor screen.</item>
    ///   <item>A framework controller that restricts nothing is recorded once, in
    ///   <see cref="ExtractedProject.FrameworkAlwaysActive"/>, rather than under all of them.
    ///   Nearly half of them restrict nothing, and repeating those under every screen would bury
    ///   the ones specific to it.</item>
    /// </list>
    /// </remarks>
    /// <param name="project">The application, whose views are updated in place.</param>
    /// <param name="catalog">The ground-truth catalog, or null to do nothing.</param>
    public static void ResolveFramework(ExtractedProject project, XafCatalog? catalog)
    {
        project.FrameworkAlwaysActive.Clear();

        if (catalog is null)
            return;

        var assemblies = FrameworkModuleScope.Resolve(project, catalog);
        var ancestry = BuildAncestry(project.Entities);
        var always = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var entry in catalog.Controllers.Values)
        {
            if (!IsLoadable(entry, assemblies) || !IsViewController(entry, catalog))
                continue;

            // Null targeting is unknown, not unrestricted, and guessing either way would be a
            // claim the catalog cannot support. It happens when the DevExpress sources are absent.
            if (entry.Targeting is not { } targeting)
                continue;

            if (targeting.IsUnrestricted)
            {
                always.Add(entry.Name);
                continue;
            }

            foreach (var view in project.Views)
            {
                if (Fits(targeting, view, ancestry) is not { } reasons)
                    continue;

                view.Activates.Add(new ViewActivation
                {
                    Controller = entry.Name,
                    SourceProject = entry.Assembly,
                    Framework = true,
                    Summary = entry.Summary,
                    DocumentationUrl = entry.DocumentationUrl,
                    Reasons = reasons,
                });
            }
        }

        project.FrameworkAlwaysActive = [.. always];
    }

    /// <summary>
    /// Whether a catalogued controller is one this application can actually instantiate.
    /// </summary>
    /// <remarks>
    /// Mirrors what XAF itself accepts when it collects controllers from an assembly: it can create
    /// an instance, and the type is not obsolete.
    /// </remarks>
    private static bool IsLoadable(XafCatalogType entry, IReadOnlySet<string> assemblies) =>
        assemblies.Contains(entry.Assembly)
        && !entry.IsAbstract
        && !entry.IsObsolete
        // A generic definition is never registered; only the classes that close it are, and those
        // are catalogued separately.
        && !entry.Name.Contains('`');

    /// <summary>
    /// Whether a controller is activated by views at all.
    /// </summary>
    /// <remarks>
    /// A <c>WindowController</c> belongs to a window, not to a view, and has none of the four view
    /// conditions. Its targeting therefore reads as "unrestricted", which would file every one of
    /// them under the controllers that run on every screen — where they do not belong.
    /// </remarks>
    private static bool IsViewController(XafCatalogType entry, XafCatalog catalog)
    {
        var current = entry;

        for (var step = 0; step < 32 && current is not null; step++)
        {
            // Reflection names a generic base ViewController`1, so the arity has to come off before
            // the chain can be recognised at all.
            var baseName = current.BaseType?.Split('`')[0];

            switch (baseName)
            {
                case "ViewController" or "ObjectViewController":
                    return true;
                case "WindowController" or "Controller" or null:
                    return false;
            }

            current = catalog.Controllers.GetValueOrDefault(current.BaseType!);
        }

        return false;
    }

    /// <summary>
    /// Controllers restricted to a view id this analysis could not resolve.
    /// </summary>
    /// <remarks>
    /// They appear on no view here, and they are real. Reporting them apart is the honest place to
    /// put an answer of "restricted, but to what could not be read from the source" — and the
    /// expression that produced the id is usually enough for a person to finish the job.
    /// </remarks>
    /// <param name="controllers">The application's controllers.</param>
    public static IEnumerable<ExtractedController> Undetermined(IReadOnlyList<ExtractedController> controllers) =>
        controllers.Where(controller => !controller.IsAbstract && controller.Targeting.IsUndetermined);

    /// <summary>
    /// Says in one phrase why a controller's activation could not be worked out.
    /// </summary>
    /// <param name="controller">A controller from <see cref="Undetermined"/>.</param>
    public static string UndeterminedReason(ExtractedController controller) =>
        controller.Targeting.UnresolvedBase is { } baseName
            ? $"extends `{baseName}`, which this analysis cannot see"
            : $"`TargetViewId = {controller.Targeting.UnresolvedViewId}`";

    /// <summary>
    /// Evaluates the four conditions, returning why it matched or null when it did not.
    /// </summary>
    private static List<ActivationReason>? Fits(
        ControllerTargeting targeting,
        ExtractedView view,
        IReadOnlyDictionary<string, HashSet<string>> ancestry)
    {
        var reasons = new List<ActivationReason>();

        // 1. Nesting. A view that can appear both ways cannot rule anything out.
        if (targeting.Nesting is { } nesting)
        {
            var required = nesting == "Root" ? ViewNesting.Root : ViewNesting.Nested;

            if (view.Nesting != ViewNesting.Either && view.Nesting != required)
                return null;

            reasons.Add(new ActivationReason { Condition = ActivationCondition.Nesting, Required = nesting });
        }

        // 2. View type: typeOfView.IsAssignableFrom(view.GetType()).
        if (targeting.TypeOfView is { } typeOfView)
        {
            if (!IsViewTypeCompatible(typeOfView, view.ViewType))
                return null;

            reasons.Add(new ActivationReason
            {
                Condition = ActivationCondition.ViewType,
                Required = typeOfView,
                Actual = typeOfView == ViewTypeName(view.ViewType) ? null : ViewTypeName(view.ViewType),
            });
        }

        // 3. Object type: assignability, not equality -- targeting a base class reaches every
        //    class derived from it. A dashboard has no object type, so any restriction excludes it.
        if (targeting.TargetObjectType is { } targetObjectType)
        {
            if (view.ObjectType is not { } objectType)
                return null;

            if (!ancestry.TryGetValue(objectType, out var chain) || !chain.Contains(targetObjectType))
                return null;

            reasons.Add(new ActivationReason
            {
                Condition = ActivationCondition.ObjectType,
                Required = targetObjectType,
                Actual = objectType == targetObjectType ? null : objectType,
            });
        }

        // 4. View id, exact match against any of the listed ids.
        //
        // Anything this analysis could not read -- an id that is not a literal, or a base class it
        // cannot see -- belongs on no view. Putting it on all of them would invent an appearance on
        // every screen in the application, which is a far bigger lie than leaving it out; and
        // Undetermined lists them, with the reason, so nothing is lost silently.
        if (targeting.IsUndetermined)
            return null;

        if (targeting.ViewIds.Count > 0)
        {
            if (!targeting.ViewIds.Contains(view.Id, StringComparer.Ordinal))
                return null;

            reasons.Add(new ActivationReason { Condition = ActivationCondition.ViewId, Required = view.Id });
        }

        return reasons;
    }

    /// <summary>
    /// Whether a view of this kind satisfies a <c>TypeOfView</c> restriction.
    /// </summary>
    /// <remarks>
    /// XAF asks <c>typeOfView.IsAssignableFrom(view.GetType())</c>, so the framework's own
    /// hierarchy decides it — verified against the 26.1 sources, because the shape is not the
    /// obvious one:
    /// <code>
    /// View
    ///  └ CompositeView
    ///     ├ ObjectView
    ///     │  ├ ListView
    ///     │  └ DetailView
    ///     └ DashboardView
    /// </code>
    /// <c>ObjectView</c> sits under <c>CompositeView</c> rather than directly under <c>View</c>, so
    /// a controller targeting <c>CompositeView</c> reaches dashboards as well — several built-in
    /// ones do, and reading that as "detail views only" would have missed them.
    /// </remarks>
    private static bool IsViewTypeCompatible(string typeOfView, ModelViewType viewType) => typeOfView switch
    {
        "ListView" => viewType == ModelViewType.ListView,
        "DetailView" => viewType == ModelViewType.DetailView,
        "DashboardView" => viewType == ModelViewType.DashboardView,
        "ObjectView" => viewType is ModelViewType.ListView or ModelViewType.DetailView,
        "CompositeView" => true,
        // A view class this application defines. Nothing here can rule it out, and excluding it
        // would drop a controller that does run.
        _ => true,
    };

    private static string ViewTypeName(ModelViewType viewType) => viewType.ToString();

    /// <summary>
    /// Maps each class to itself and everything it derives from.
    /// </summary>
    /// <remarks>
    /// Includes base names that are not classes of this application — <c>BaseObject</c> and the
    /// like — because a controller targeting one of those genuinely does run on every view of every
    /// class beneath it, and that is exactly the surprise worth reporting.
    /// </remarks>
    private static Dictionary<string, HashSet<string>> BuildAncestry(IReadOnlyList<ExtractedEntity> entities)
    {
        var baseOf = entities.ToDictionary(e => e.ClassName, e => e.BaseType, StringComparer.Ordinal);
        var ancestry = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var entity in entities)
        {
            var chain = new HashSet<string>(StringComparer.Ordinal) { entity.ClassName };
            var current = entity.BaseType;

            // Bounded by the number of classes, so a cycle in source that does not compile cannot
            // spin here.
            for (var step = 0; step < entities.Count + 1 && !string.IsNullOrWhiteSpace(current); step++)
            {
                if (!chain.Add(current))
                    break;

                current = baseOf.GetValueOrDefault(current, string.Empty);
            }

            ancestry[entity.ClassName] = chain;
        }

        return ancestry;
    }
}
