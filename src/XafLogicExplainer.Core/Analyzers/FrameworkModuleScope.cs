using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Decides which of the framework's controllers this application actually has.
/// </summary>
/// <remarks>
/// The catalog knows every controller DevExpress ships — 386 of them on 26.1 — and an application
/// loads a fraction. A controller only exists if the module that carries it was registered, so the
/// modules named in <c>RequiredModuleTypes</c> decide the set. Skipping that step would report
/// WinForms controllers on a Blazor application's screens, which is worse than reporting none.
/// </remarks>
public static class FrameworkModuleScope
{
    /// <summary>
    /// The assembly every XAF application has, whether or not anyone wrote it down.
    /// </summary>
    /// <remarks>
    /// <c>SystemModule</c> is added by the framework itself, so it is absent from the module class
    /// of every application while being present in all of them.
    /// </remarks>
    private static readonly string[] Always =
    [
        "DevExpress.ExpressApp",
        "DevExpress.Persistent.Base",
    ];

    /// <summary>
    /// Works out which framework assemblies contribute controllers to this application.
    /// </summary>
    /// <param name="project">The extracted application.</param>
    /// <param name="catalog">The ground-truth catalog.</param>
    public static HashSet<string> Resolve(ExtractedProject project, XafCatalog catalog)
    {
        var assemblies = new HashSet<string>(Always, StringComparer.Ordinal);

        foreach (var required in project.ModuleInfo?.RequiredModules ?? [])
        {
            var name = LastSegment(required);

            if (catalog.Modules.TryGetValue(name, out var module) && module.Assembly.Length > 0)
                assemblies.Add(module.Assembly);
        }

        // The platform module is registered by the application builder, not by the module class, so
        // it is named in no source this extraction reads. The platform project beside the module is
        // the evidence that it is there.
        foreach (var platform in PlatformAssemblies(project))
            assemblies.Add(platform);

        return assemblies;
    }

    /// <summary>
    /// Names the platform assemblies implied by the projects sitting beside the module.
    /// </summary>
    private static IEnumerable<string> PlatformAssemblies(ExtractedProject project)
    {
        var projects = project.Controllers.Select(c => c.SourceProject)
            .Concat(project.Editors.Select(e => e.SourceProject))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToList();

        // A whole dotted segment, not a substring. `Contains("Win")` matched `Winery.Module` and
        // `Darwin.Core`, and pulled every WinForms controller onto a Blazor application's screens
        // -- the exact failure this file exists to prevent.
        if (projects.Any(name => HasSegment(name, "Blazor")))
            yield return "DevExpress.ExpressApp.Blazor";

        if (projects.Any(name => HasSegment(name, "Win")))
            yield return "DevExpress.ExpressApp.Win";
    }

    /// <summary>
    /// Whether a project name contains the given dotted segment, e.g. <c>Shop.Win.Server</c>.
    /// </summary>
    private static bool HasSegment(string projectName, string segment) =>
        projectName.Split('.').Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));

    private static string LastSegment(string typeName)
    {
        var name = typeName.Trim();
        var lastDot = name.LastIndexOf('.');

        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }
}
