using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Completes each controller's targeting with what it inherits from the one it extends.
/// </summary>
/// <remarks>
/// A controller declares where it activates in its constructor, and constructors run base-first, so
/// a class that adds an action to <c>MyListViewControllerBase</c> and says nothing else is still
/// restricted to list views. Reading each class on its own reports it as running everywhere.
/// <para>
/// It has to run after everything is parsed, because the base may be a class in the same project,
/// a class in a sibling project, or one DevExpress ships — and the answer for the third only exists
/// when a catalog is present.
/// </para>
/// </remarks>
public static class ControllerTargetingResolver
{
    /// <summary>
    /// Resolves an application's controllers against each other and the framework.
    /// </summary>
    /// <param name="controllers">The extracted controllers, completed in place.</param>
    /// <param name="catalog">The framework catalog, or null when this machine has none.</param>
    public static void Resolve(IReadOnlyList<ExtractedController> controllers, XafCatalog? catalog)
    {
        var byName = new Dictionary<string, ExtractedController>(StringComparer.Ordinal);

        foreach (var controller in controllers)
            byName.TryAdd(controller.ClassName, controller);

        var done = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);

        foreach (var controller in controllers)
            Resolve(controller, byName, catalog, done, visiting);
    }

    /// <summary>
    /// Resolves a catalog's controllers against each other.
    /// </summary>
    /// <remarks>
    /// Run before a catalog is written, so what is stored is the effective answer and a reader does
    /// not have to walk the chain again.
    /// </remarks>
    /// <param name="catalog">The catalog, completed in place.</param>
    /// <returns>How many controllers gained a condition from a base class.</returns>
    public static int Resolve(XafCatalog catalog)
    {
        var done = new HashSet<string>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var before = catalog.Controllers.Values.Count(entry => entry.Targeting is { IsUnrestricted: false });

        foreach (var name in catalog.Controllers.Keys)
            Resolve(catalog, name, done, visiting);

        return catalog.Controllers.Values.Count(entry => entry.Targeting is { IsUnrestricted: false }) - before;
    }

    private static void Resolve(
        ExtractedController controller,
        Dictionary<string, ExtractedController> byName,
        XafCatalog? catalog,
        HashSet<string> done,
        HashSet<string> visiting)
    {
        // visiting guards against a cycle, which cannot occur in code that compiles but can in
        // source that does not -- and this analysis is built to run on source that does not.
        if (!done.Add(controller.ClassName) || !visiting.Add(controller.ClassName))
            return;

        var baseName = StripGenerics(controller.BaseControllerType);

        if (byName.TryGetValue(baseName, out var baseController))
        {
            Resolve(baseController, byName, catalog, done, visiting);
            ControllerTargetingReader.Inherit(controller.Targeting, baseController.Targeting);
        }
        else if (catalog?.FindController(baseName)?.Targeting is { } frameworkTargeting)
        {
            // Already effective: a catalog stores targeting with its own inheritance resolved.
            ControllerTargetingReader.Inherit(controller.Targeting, frameworkTargeting);
        }

        visiting.Remove(controller.ClassName);
    }

    private static void Resolve(XafCatalog catalog, string name, HashSet<string> done, HashSet<string> visiting)
    {
        if (!done.Add(name) || !visiting.Add(name))
            return;

        if (catalog.Controllers.TryGetValue(name, out var entry)
            && entry.Targeting is { } targeting
            && entry.BaseType is { Length: > 0 } baseName)
        {
            Resolve(catalog, baseName, done, visiting);

            if (catalog.Controllers.TryGetValue(baseName, out var baseEntry) && baseEntry.Targeting is { } inherited)
                ControllerTargetingReader.Inherit(targeting, inherited);
        }

        visiting.Remove(name);
    }

    private static string StripGenerics(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var name = typeName.Trim();
        var generic = name.IndexOf('<');

        if (generic > 0)
            name = name[..generic];

        var lastDot = name.LastIndexOf('.');

        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }
}
