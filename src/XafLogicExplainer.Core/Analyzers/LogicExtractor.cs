using XafLogicExplainer.Core.Hashing;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Coordinates all specialized analyzers to produce one complete <see cref="ExtractedProject"/> snapshot.
/// </summary>
public class LogicExtractor : ILogicExtractor
{
    private readonly IEntityAnalyzer _entityAnalyzer;
    private readonly IControllerAnalyzer _controllerAnalyzer;
    private readonly UpdaterAnalyzer _updaterAnalyzer;
    private readonly ModuleAnalyzer _moduleAnalyzer;
    private readonly ModelAnalyzer _modelAnalyzer;
    private readonly ProjectHashCalculator _hashCalculator;

    /// <summary>
    /// Creates a default extractor with built-in analyzer implementations.
    /// </summary>
    public LogicExtractor()
    {
        _entityAnalyzer = new EntityAnalyzer();
        _controllerAnalyzer = new ControllerAnalyzer();
        _updaterAnalyzer = new UpdaterAnalyzer();
        _moduleAnalyzer = new ModuleAnalyzer();
        _modelAnalyzer = new ModelAnalyzer();
        _hashCalculator = new ProjectHashCalculator();
    }

    /// <summary>
    /// Creates an extractor with explicit analyzer dependencies.
    /// </summary>
    /// <param name="entityAnalyzer">Entity analyzer implementation.</param>
    /// <param name="controllerAnalyzer">Controller analyzer implementation.</param>
    /// <param name="updaterAnalyzer">Updater analyzer implementation.</param>
    /// <param name="moduleAnalyzer">Module analyzer implementation.</param>
    /// <param name="modelAnalyzer">Model analyzer implementation.</param>
    public LogicExtractor(
        IEntityAnalyzer entityAnalyzer,
        IControllerAnalyzer controllerAnalyzer,
        UpdaterAnalyzer updaterAnalyzer,
        ModuleAnalyzer moduleAnalyzer,
        ModelAnalyzer modelAnalyzer)
    {
        _entityAnalyzer = entityAnalyzer;
        _controllerAnalyzer = controllerAnalyzer;
        _updaterAnalyzer = updaterAnalyzer;
        _moduleAnalyzer = moduleAnalyzer;
        _modelAnalyzer = modelAnalyzer;
        _hashCalculator = new ProjectHashCalculator();
    }

    /// <summary>
    /// Runs end-to-end extraction for the provided XAF source directory.
    /// </summary>
    /// <param name="projectPath">Project root path to analyze.</param>
    /// <param name="options">Optional extraction settings; defaults are used when null.</param>
    /// <returns>Aggregated extraction result with entities, controllers, and model metadata.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the provided directory does not exist.</exception>
    public ExtractedProject ExtractFromSourceDirectory(string projectPath, ExtractionOptions? options = null)
    {
        options ??= new ExtractionOptions();

        if (!Directory.Exists(projectPath))
            throw new DirectoryNotFoundException($"Project directory not found: {projectPath}");

        var project = new ExtractedProject
        {
            ProjectName = InferProjectName(projectPath),
            ProjectPath = projectPath,
            ExtractedAt = DateTime.UtcNow.ToString("o"),
            SourceHash = _hashCalculator.ComputeHash(projectPath),
        };

        // Extract project metadata from .csproj
        ExtractProjectMetadata(projectPath, project);

        // 1. Extract entities (business objects) from Module
        project.Entities = _entityAnalyzer.AnalyzeEntities(projectPath, options);
        foreach (var e in project.Entities)
            e.SourceProject ??= new DirectoryInfo(projectPath).Name;

        // Set detected ORM type
        project.OrmType = options.ResolvedOrm == OrmType.EfCore ? "EF Core" : "XPO";

        // 2. Extract controllers from Module
        project.Controllers = _controllerAnalyzer.AnalyzeControllers(projectPath, options);
        foreach (var c in project.Controllers)
            c.SourceProject ??= new DirectoryInfo(projectPath).Name;

        // 2.5. Discover sibling platform projects and extract their entities/controllers
        if (options.DiscoverPlatformModels)
        {
            var siblings = DiscoverSiblingDirectories(projectPath);
            foreach (var siblingDir in siblings)
            {
                var siblingName = new DirectoryInfo(siblingDir).Name;

                var siblingEntities = _entityAnalyzer.AnalyzeEntities(siblingDir, options);
                foreach (var e in siblingEntities)
                    e.SourceProject = siblingName;

                // Merge: add only entities not already found in Module (deduplicate by ClassName)
                var existingNames = new HashSet<string>(project.Entities.Select(e => e.ClassName));
                foreach (var e in siblingEntities)
                {
                    if (!existingNames.Contains(e.ClassName))
                        project.Entities.Add(e);
                }

                var siblingControllers = _controllerAnalyzer.AnalyzeControllers(siblingDir, options);
                foreach (var c in siblingControllers)
                    c.SourceProject = siblingName;

                // Merge: add only controllers not already found in Module
                var existingControllers = new HashSet<string>(project.Controllers.Select(c => c.ClassName));
                foreach (var c in siblingControllers)
                {
                    if (!existingControllers.Contains(c.ClassName))
                        project.Controllers.Add(c);
                }
            }
        }

        // 3. Extract seed data from Updater
        project.SeedData = _updaterAnalyzer.AnalyzeUpdater(projectPath, options);

        // 4. Extract module info
        project.ModuleInfo = _moduleAnalyzer.AnalyzeModule(projectPath, options);

        // 5. Build navigation structure from entities
        project.Navigation = BuildNavigationStructure(project.Entities);

        // 6. Extract Model Editor customizations (xafml)
        var modelInfo = _modelAnalyzer.AnalyzeModel(projectPath, options);
        project.ModelEditorInfo = modelInfo;

        // 7. Enrich entities with BOModel data
        if (modelInfo != null)
            EnrichEntitiesFromModel(project.Entities, modelInfo.BOModelClasses);

        return project;
    }

    /// <summary>
    /// Builds the navigation grouping structure from extracted entity metadata.
    /// </summary>
    private static List<ExtractedNavigationItem> BuildNavigationStructure(List<ExtractedEntity> entities)
    {
        return entities
            .Where(e => !string.IsNullOrEmpty(e.NavigationGroup))
            .GroupBy(e => e.NavigationGroup!)
            .Select(g => new ExtractedNavigationItem
            {
                GroupName = g.Key,
                EntityClassNames = g.Select(e => e.ClassName).ToList()
            })
            .ToList();
    }

    /// <summary>
    /// Parses project-level metadata from the main csproj file.
    /// </summary>
    private static void ExtractProjectMetadata(string projectPath, ExtractedProject project)
    {
        var csprojFiles = Directory.GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (csprojFiles.Length == 0) return;

        var csprojContent = File.ReadAllText(csprojFiles[0]);

        // HACK: Metadata parsing currently relies on simple regex patterns against raw XML text.
        // A future refactor should use XDocument to reliably handle multiline attributes and property groups.
        // Extract TargetFramework
        var tfmMatch = System.Text.RegularExpressions.Regex.Match(csprojContent,
            @"<TargetFramework>(.*?)</TargetFramework>");
        if (tfmMatch.Success)
            project.TargetFramework = tfmMatch.Groups[1].Value;

        // Extract PackageReferences
        var packageMatches = System.Text.RegularExpressions.Regex.Matches(csprojContent,
            @"<PackageReference\s+Include=""(.*?)""\s+Version=""(.*?)""");
        foreach (System.Text.RegularExpressions.Match match in packageMatches)
        {
            project.PackageReferences.Add($"{match.Groups[1].Value} {match.Groups[2].Value}");
        }
    }

    /// <summary>
    /// Enriches entities with BOModel metadata sourced from xafml.
    /// </summary>
    private static void EnrichEntitiesFromModel(List<ExtractedEntity> entities, List<ModelClassInfo> modelClasses)
    {
        foreach (var modelClass in modelClasses)
        {
            var entity = entities.FirstOrDefault(e =>
                e.ClassName == modelClass.ClassName ||
                $"{e.Namespace}.{e.ClassName}" == modelClass.FullName);

            if (entity == null) continue;

            if (!string.IsNullOrEmpty(modelClass.Caption))
                entity.ModelCaption = modelClass.Caption;

            if (modelClass.IsCloneable)
                entity.IsCloneable = true;
        }
    }

    /// <summary>
    /// Discovers sibling project directories that may contain additional entities/controllers.
    /// Uses the same parent-directory pattern as ModelAnalyzer for xafml discovery.
    /// </summary>
    private static List<string> DiscoverSiblingDirectories(string moduleDirectory)
    {
        var siblings = new List<string>();

        var parentDir = Directory.GetParent(moduleDirectory)?.FullName;
        if (parentDir == null) return siblings;

        foreach (var siblingDir in Directory.GetDirectories(parentDir))
        {
            // Skip the module directory itself
            if (siblingDir.Equals(moduleDirectory, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip common non-project directories
            var dirName = Path.GetFileName(siblingDir);
            if (dirName.StartsWith(".") || dirName == "packages" || dirName == "node_modules")
                continue;

            // Only include siblings that have at least one .cs file (actual project dirs)
            try
            {
                if (Directory.GetFiles(siblingDir, "*.cs", SearchOption.AllDirectories)
                    .Any(f => BuildOutputFilter.IsAnalyzable(f, siblingDir)))
                {
                    siblings.Add(siblingDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible directories
            }
        }

        return siblings;
    }

    /// <summary>
    /// Infers a normalized project name from the source directory.
    /// </summary>
    private static string InferProjectName(string projectPath)
    {
        var dirName = new DirectoryInfo(projectPath).Name;
        // Remove common suffixes
        return dirName
            .Replace(".Module", "")
            .Replace(".Blazor.Server", "")
            .Replace(".Win", "");
    }
}
