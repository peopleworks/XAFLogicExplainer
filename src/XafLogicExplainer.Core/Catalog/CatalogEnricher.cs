using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Catalog;

/// <summary>
/// Annotates an extracted application with what the framework, rather than the team, provides.
/// </summary>
/// <remarks>
/// Runs after extraction rather than inside the analyzers, so the catalog stays entirely optional:
/// with none present nothing here executes and the result is what it always was.
/// <para>
/// It answers one question the source alone cannot: which of these names came with XAF? A
/// controller deriving from <c>DeleteObjectsViewController</c> is extending shipped behavior; one
/// deriving from a class in the same project is not, and an agent asked to change the first should
/// know it is looking at framework code.
/// </para>
/// </remarks>
public static class CatalogEnricher
{
    /// <summary>
    /// Applies catalog knowledge to an extracted project.
    /// </summary>
    /// <param name="project">The project to annotate, modified in place.</param>
    /// <param name="catalog">The catalog, or null to do nothing.</param>
    public static void Enrich(ExtractedProject project, XafCatalog? catalog)
    {
        if (catalog is null)
            return;

        project.CatalogVersion = catalog.DevExpressVersion;

        EnrichControllers(project, catalog);
        EnrichAttributes(project, catalog);
    }

    /// <summary>
    /// The generic entry points every XAF controller starts from.
    /// </summary>
    /// <remarks>
    /// Deriving from one of these carries no information: it says "this is a controller", which
    /// the reader already knows from the word Controller in its name. Reporting them turned the
    /// section into a list of every controller in the application, each annotated "A View
    /// Controller" — noise that buries the one case worth noticing.
    /// </remarks>
    private static readonly HashSet<string> GenericControllerBases = new(StringComparer.Ordinal)
    {
        "Controller",
        "ViewController",
        "ObjectViewController",
        "WindowController",
    };

    private static void EnrichControllers(ExtractedProject project, XafCatalog catalog)
    {
        var ownControllers = project.Controllers
            .Select(c => c.ClassName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var controller in project.Controllers)
        {
            // A base type defined in this same project is the team's own layering, not framework
            // behavior, even when the catalog happens to hold a type of that name.
            if (ownControllers.Contains(StripGenerics(controller.BaseControllerType)))
                continue;

            var frameworkBase = catalog.FindController(controller.BaseControllerType);

            if (frameworkBase is null || GenericControllerBases.Contains(frameworkBase.Name))
                continue;

            // What remains is a controller extending a specific piece of shipped behavior --
            // DeleteObjectsViewController, ExportController -- which changes how an existing
            // feature works rather than adding a new one.
            controller.FrameworkBaseType = frameworkBase.Name;
            controller.FrameworkBaseSummary = frameworkBase.Summary;
            controller.FrameworkBaseDocumentationUrl = frameworkBase.DocumentationUrl;
        }
    }

    /// <summary>
    /// Separates the attributes XAF defines from the ones this application defines itself.
    /// </summary>
    /// <remarks>
    /// A custom attribute is a signal worth surfacing: it usually marks a convention the team
    /// invented, which an agent has no other way to learn about and cannot look up in any
    /// documentation.
    /// </remarks>
    private static void EnrichAttributes(ExtractedProject project, XafCatalog catalog)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var custom = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var entity in project.Entities)
        {
            foreach (var attributeName in entity.Properties.SelectMany(p => p.CustomAttributes))
            {
                var name = NormalizeAttributeName(attributeName);

                if (name.Length == 0 || !seen.Add(name))
                    continue;

                if (catalog.FindAttribute(name) is null && !IsFrameworkNamespace(name))
                    custom.Add(name);
            }
        }

        project.CustomAttributes = [.. custom];
    }

    /// <summary>
    /// Reduces an attribute as written in source to its bare name.
    /// </summary>
    private static string NormalizeAttributeName(string attribute)
    {
        var name = attribute.Trim();

        // "[RuleRequiredField("id", DefaultContexts.Save)]" -> "RuleRequiredField"
        var parenthesis = name.IndexOf('(');
        if (parenthesis > 0)
            name = name[..parenthesis];

        name = name.Trim('[', ']', ' ');

        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name[(lastDot + 1)..];

        return name.Trim();
    }

    /// <summary>
    /// Whether a name is a .NET attribute that no XAF catalog would list.
    /// </summary>
    /// <remarks>
    /// EF Core applications annotate with <c>System.ComponentModel.DataAnnotations</c>, which is
    /// part of .NET rather than of XAF. Reporting those as "your own" would be wrong and would
    /// bury the handful that genuinely are.
    /// </remarks>
    private static bool IsFrameworkNamespace(string attributeName) =>
        attributeName is
            "Required" or "StringLength" or "MaxLength" or "MinLength" or "Range" or
            "Key" or "ForeignKey" or "NotMapped" or "Column" or "Table" or "InverseProperty" or
            "Description" or "DisplayName" or "Browsable" or "DefaultValue" or "Obsolete" or
            "DataType" or "Display" or "Editable" or "Compare" or "RegularExpression" or
            "EmailAddress" or "Phone" or "Url" or "CreditCard" or "Timestamp" or "ConcurrencyCheck";

    private static string StripGenerics(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var generic = typeName.IndexOf('<');
        return generic > 0 ? typeName[..generic] : typeName;
    }
}
