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

        // Resolve the ground-truth catalog before anything reads source, and hand it to the
        // analyzers through the options they already carry. Controller extraction needs it to
        // recognise a class that extends a DevExpress controller, which is not something it can
        // work out on its own.
        var catalog = options.Catalog ?? (options.UseCatalog ? Catalog.XafCatalogStore.LoadLatest() : null);
        options.Catalog = catalog;

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

        // 2.6. Custom editors. Nearly always in a platform project beside the module, which is
        //      exactly why nobody reading the business objects ever meets them.
        var editorAnalyzer = new EditorAnalyzer();
        var siblingDirs = options.DiscoverPlatformModels
            ? DiscoverSiblingDirectories(projectPath).ToList()
            : [];

        // Alias constants are gathered across the whole solution before any editor is read. The
        // editor lives in the platform project and names a constant declared in the module beside
        // it, so a project read on its own resolves nothing and reports the expression verbatim.
        var constants = EditorAnalyzer.CollectStringConstants(projectPath);
        foreach (var siblingDir in siblingDirs)
        {
            foreach (var (key, value) in EditorAnalyzer.CollectStringConstants(siblingDir))
                constants.TryAdd(key, value);
        }

        project.Editors.AddRange(Stamp(
            editorAnalyzer.AnalyzeEditors(projectPath, options, constants),
            new DirectoryInfo(projectPath).Name));

        foreach (var siblingDir in siblingDirs)
        {
            var siblingName = new DirectoryInfo(siblingDir).Name;
            var known = project.Editors.Select(e => e.ClassName).ToHashSet(StringComparer.Ordinal);

            foreach (var editor in Stamp(editorAnalyzer.AnalyzeEditors(siblingDir, options, constants), siblingName))
            {
                if (known.Add(editor.ClassName))
                    project.Editors.Add(editor);
            }
        }

        LinkEditorsToProperties(project);

        // 3. Extract seed data from Updater, and the version-gated blocks beside it. The second
        //    kind never runs again on a database that has already been upgraded, which is exactly
        //    why nothing else can recover what it did.
        project.SeedData = _updaterAnalyzer.AnalyzeUpdater(projectPath, options);
        project.Migrations = _updaterAnalyzer.AnalyzeMigrations(projectPath, options);

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

        // 8. Annotate with the DevExpress ground-truth catalog, if this machine has one.
        //    Optional by design: no catalog means this step does nothing at all.
        Catalog.CatalogEnricher.Enrich(project, catalog);

        // 9. Complete each controller's targeting with what it inherits from the controller it
        //    extends. Needs every controller parsed first, so it cannot live in the analyzer.
        ControllerTargetingResolver.Resolve(project.Controllers, catalog);

        // 10. Enumerate the screens, and work out what runs on each. Last, because it is a view of
        //     everything above it rather than a new reading of the source.
        project.Views = ViewInventory.Build(project);
        ViewInventory.ResolveModelOnlyObjectTypes(project.Views, project.Entities);
        ViewActivationResolver.Resolve(project.Views, project.Controllers, project.Entities);

        // The framework's own controllers, scoped to the modules this application registers. Adds
        // nothing at all without a catalog, which is the state of every machine that has not built
        // one.
        ViewActivationResolver.ResolveFramework(project, catalog);

        return project;
    }

    /// <summary>Records which project each editor came from.</summary>
    private static List<ExtractedEditor> Stamp(List<ExtractedEditor> editors, string projectName)
    {
        foreach (var editor in editors)
            editor.SourceProject ??= projectName;

        return editors;
    }

    /// <summary>
    /// Works out which properties each custom editor actually renders.
    /// </summary>
    /// <remarks>
    /// An editor on its own is a curiosity; an editor attached to a property somebody is about to
    /// change is a warning. But the second argument of the registration attribute decides which
    /// of those it is, and getting that backwards is worse than saying nothing.
    /// <para>
    /// <c>isDefault: true</c> means XAF uses the editor for every property of that type, so every
    /// one of them is genuinely affected. <c>false</c> means it is merely <em>selectable</em> in
    /// the Model Editor — listing every string property in the application as "uses the barcode
    /// scanner" would be plainly false. Those are linked only where a property asks for the alias
    /// by name.
    /// </para>
    /// <para>
    /// A registration for <c>typeof(object)</c> claims everything and is never linked: naming
    /// every property in the application communicates nothing.
    /// </para>
    /// </remarks>
    /// <param name="project">The extracted project, annotated in place.</param>
    public static void LinkEditorsToProperties(ExtractedProject project)
    {
        foreach (var editor in project.Editors)
        {
            var target = editor.TargetType;
            var claimsEverything = target is "object" or "Object";
            var appliesByType = editor.IsDefault && !claimsEverything && !string.IsNullOrWhiteSpace(target);

            foreach (var entity in project.Entities)
            {
                var matches = entity.Properties.Any(p =>
                    (!string.IsNullOrWhiteSpace(editor.Alias) &&
                     string.Equals(p.EditorAlias, editor.Alias, StringComparison.Ordinal))
                    || (appliesByType &&
                        string.Equals(StripGeneric(p.TypeName), target, StringComparison.Ordinal)));

                if (matches)
                    editor.UsedBy.Add(entity.ClassName);
            }
        }
    }

    /// <summary>Turns <c>XPCollection&lt;OrderLine&gt;</c> into <c>OrderLine</c>.</summary>
    private static string StripGeneric(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var open = typeName.IndexOf('<');
        var close = typeName.LastIndexOf('>');

        return open >= 0 && close > open ? typeName[(open + 1)..close] : typeName;
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
