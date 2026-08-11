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
    /// Controllers restricted to a view id this analysis could not resolve.
    /// </summary>
    /// <remarks>
    /// They appear on no view here, and they are real. Reporting them apart is the honest place to
    /// put an answer of "restricted, but to what could not be read from the source" — and the
    /// expression that produced the id is usually enough for a person to finish the job.
    /// </remarks>
    /// <param name="controllers">The application's controllers.</param>
    public static IEnumerable<ExtractedController> Undetermined(IReadOnlyList<ExtractedController> controllers) =>
        controllers.Where(controller => controller.Targeting.UnresolvedViewId is not null);

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
        // A controller restricted to an id this analysis could not read belongs on no view. Putting
        // it on all of them would invent an appearance on every screen in the application, which is
        // a far bigger lie than leaving it out -- and Undetermined lists them so it is not lost.
        if (targeting.UnresolvedViewId is not null)
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
    /// XAF asks <c>typeOfView.IsAssignableFrom(view.GetType())</c>, and <c>ListView</c> and
    /// <c>DetailView</c> both derive from <c>ObjectView</c> — which is why a controller targeting
    /// <c>ObjectView</c>, as several built-in ones do, appears on both and on neither dashboard.
    /// </remarks>
    private static bool IsViewTypeCompatible(string typeOfView, ModelViewType viewType) => typeOfView switch
    {
        "ListView" => viewType == ModelViewType.ListView,
        "DetailView" => viewType == ModelViewType.DetailView,
        "DashboardView" => viewType == ModelViewType.DashboardView,
        "ObjectView" => viewType is ModelViewType.ListView or ModelViewType.DetailView,
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
