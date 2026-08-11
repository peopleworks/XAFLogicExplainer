using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Conventions inferred from what a specific codebase actually does.
/// </summary>
/// <remarks>
/// An agent that has read the XAF documentation knows the framework's options. It does not know
/// which of them <em>this</em> team chose, and generic advice is exactly what produces code that
/// compiles but looks foreign in review.
/// <para>
/// Everything here is observed, never assumed: each property is derived from the extracted model,
/// and anything that cannot be established from the source is left null rather than guessed.
/// </para>
/// </remarks>
public sealed class CodebaseConventions
{
    /// <summary>Longest namespace prefix shared by all entities, if there is one.</summary>
    public string? EntityNamespace { get; init; }

    /// <summary>Directory (relative to the project) where most entities live.</summary>
    public string? EntityFolder { get; init; }

    /// <summary>Directory (relative to the project) where most controllers live.</summary>
    public string? ControllerFolder { get; init; }

    /// <summary>
    /// Every folder controllers were found in, most populated first.
    /// </summary>
    /// <remarks>
    /// An XAF solution routinely keeps controllers in two places: the module for anything
    /// platform-independent, and the platform project for anything that touches its UI. Reporting
    /// only the most common one is a coin flip when there are two of each, and it sent the
    /// instruction "put the controller here" at the platform project of an application whose only
    /// action controller lives in the module.
    /// </remarks>
    public IReadOnlyList<string> ControllerFolders { get; init; } = [];

    /// <summary>The base class most persistent classes derive from.</summary>
    public string? DominantEntityBaseType { get; init; }

    /// <summary>The base class most controllers derive from.</summary>
    public string? DominantControllerBaseType { get; init; }

    /// <summary>True when relationships are declared with explicit association names.</summary>
    public bool UsesNamedAssociations { get; init; }

    /// <summary>True when the codebase has calculated properties via PersistentAlias.</summary>
    public bool UsesPersistentAlias { get; init; }

    /// <summary>True when validation is expressed through XAF rule attributes.</summary>
    public bool UsesValidationAttributes { get; init; }

    /// <summary>True when Conditional Appearance attributes are in use.</summary>
    public bool UsesAppearanceRules { get; init; }

    /// <summary>True when the Model Editor holds customizations that exist in no C# file.</summary>
    public bool HasModelEditorCustomizations { get; init; }

    /// <summary>Distinct criteria expressions found in source, most useful first.</summary>
    public IReadOnlyList<CriteriaExample> CriteriaExamples { get; init; } = [];

    /// <summary>
    /// Infers conventions from an extracted project.
    /// </summary>
    public static CodebaseConventions Infer(ExtractedProject project)
    {
        var entities = project.Entities;
        var controllers = project.Controllers;

        return new CodebaseConventions
        {
            EntityNamespace = MostCommon(entities.Select(e => e.Namespace)),
            EntityFolder = MostCommon(entities.Select(e => FolderOf(e.FilePath, project.ProjectPath))),
            ControllerFolder = MostCommon(controllers.Select(c => FolderOf(c.FilePath, project.ProjectPath))),
            ControllerFolders =
            [
                .. controllers
                    .Select(c => FolderOf(c.FilePath, project.ProjectPath))
                    .Where(folder => !string.IsNullOrWhiteSpace(folder))
                    .GroupBy(folder => folder!, StringComparer.Ordinal)
                    .OrderByDescending(group => group.Count())
                    .ThenBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => group.Key),
            ],
            DominantEntityBaseType = MostCommon(entities.Select(e => e.BaseType)),
            DominantControllerBaseType = MostCommon(controllers.Select(c => c.BaseControllerType)),
            UsesNamedAssociations = entities.Any(e =>
                e.Relationships.Any(r => !string.IsNullOrWhiteSpace(r.AssociationName))),
            UsesPersistentAlias = entities.Any(e =>
                e.Properties.Any(p => !string.IsNullOrWhiteSpace(p.PersistentAlias))),
            UsesValidationAttributes = entities.Any(e => e.ValidationRules.Count > 0),
            UsesAppearanceRules = entities.Any(e => e.AppearanceRules.Count > 0),
            HasModelEditorCustomizations =
                project.ModelEditorInfo is { } m &&
                (m.BOModelClasses.Count > 0 || m.Views.Count > 0),
            CriteriaExamples = CollectCriteria(project),
        };
    }

    /// <summary>
    /// Gathers the criteria expressions the codebase actually uses.
    /// </summary>
    /// <remarks>
    /// XAF's criteria language is a DSL of its own, and it is where agents most reliably produce
    /// plausible nonsense — inventing SQL, C# lambdas, or LINQ where a criteria string belongs.
    /// Real expressions from the codebase teach the dialect far better than a description of it.
    /// </remarks>
    private static List<CriteriaExample> CollectCriteria(ExtractedProject project)
    {
        var found = new List<CriteriaExample>();

        foreach (var entity in project.Entities)
        {
            // Both halves of a validation rule. The expression is what must be true; the target
            // criteria is when the rule applies at all -- and only the second was gathered, so the
            // expression a user actually hits was missing from the index.
            foreach (var rule in entity.ValidationRules.Where(r => !string.IsNullOrWhiteSpace(r.Expression)))
                found.Add(new CriteriaExample(rule.Expression!, $"{entity.ClassName} validation ({rule.RuleType})"));

            foreach (var rule in entity.ValidationRules.Where(r => !string.IsNullOrWhiteSpace(r.TargetCriteria)))
                found.Add(new CriteriaExample(rule.TargetCriteria!, $"{entity.ClassName} validation applies when"));

            foreach (var rule in entity.AppearanceRules.Where(r => !string.IsNullOrWhiteSpace(r.Criteria)))
                found.Add(new CriteriaExample(rule.Criteria!, $"{entity.ClassName} appearance ({rule.Id})"));

            foreach (var prop in entity.Properties.Where(p => !string.IsNullOrWhiteSpace(p.DataSourceCriteria)))
                found.Add(new CriteriaExample(prop.DataSourceCriteria!, $"{entity.ClassName}.{prop.Name} lookup filter"));
        }

        foreach (var controller in project.Controllers)
        {
            foreach (var action in controller.Actions.Where(a => !string.IsNullOrWhiteSpace(a.EnabledCriteria)))
                found.Add(new CriteriaExample(action.EnabledCriteria!, $"{action.ActionId} enabled when"));

            // What decides whether the button can be pressed. On a demo whose single action is
            // guarded by "Not IsDispensed", this was the one expression that governed the whole
            // application, and it was in no generated document.
            foreach (var action in controller.Actions.Where(a => !string.IsNullOrWhiteSpace(a.TargetObjectsCriteria)))
                found.Add(new CriteriaExample(action.TargetObjectsCriteria!, $"{action.ActionId} available when"));
        }

        foreach (var view in project.Views.Where(v => !string.IsNullOrWhiteSpace(v.Criteria)))
            found.Add(new CriteriaExample(view.Criteria!, $"{view.Id} filter"));

        // Distinct by the expression itself: the same criteria repeated across ten entities teaches
        // nothing new the second time, and the list is meant to be read, not to be exhaustive.
        //
        // Constants like "1=1" are dropped. They occur in real code -- as a placeholder for an
        // appearance rule that always applies -- but they reference nothing, so as a worked example
        // of the dialect they are pure noise, and sorting by length puts them first.
        return found
            .Where(c => c.Expression.Any(char.IsLetter))
            .GroupBy(c => c.Expression, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(c => c.Expression.Length)
            .ToList();
    }

    /// <summary>
    /// Returns the most frequent non-empty value, or null when there is nothing to report.
    /// </summary>
    private static string? MostCommon(IEnumerable<string?> values)
    {
        var winner = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .GroupBy(v => v!, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();

        return winner?.Key;
    }

    /// <summary>
    /// Reduces an absolute source path to a project-relative directory, using forward slashes so
    /// the generated documentation reads the same on every platform.
    /// </summary>
    private static string? FolderOf(string filePath, string projectPath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            try
            {
                var relative = Path.GetRelativePath(projectPath, directory);
                // GetRelativePath walks upward with ".." when the file sits outside the project --
                // which happens for entities pulled in from a sibling project. An upward path is
                // not a convention worth reporting, so drop it.
                if (!relative.StartsWith("..", StringComparison.Ordinal) && relative != ".")
                    directory = relative;
                else if (relative == ".")
                    return null;
            }
            catch (ArgumentException)
            {
                // Paths on different roots. Fall through to the directory name.
            }
        }

        return directory.Replace('\\', '/').Trim('/');
    }
}

/// <summary>
/// One criteria expression observed in the codebase, with where it came from.
/// </summary>
/// <param name="Expression">The criteria string exactly as written in source.</param>
/// <param name="Context">Human-readable origin, e.g. "Invoice validation (RuleCriteria)".</param>
public sealed record CriteriaExample(string Expression, string Context);
