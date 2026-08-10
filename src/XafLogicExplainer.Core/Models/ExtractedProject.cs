namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Root extraction output that aggregates entities, controllers, navigation, and metadata.
/// </summary>
public class ExtractedProject
{
    /// <summary>
    /// Logical project name inferred from folder or module naming convention.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Source root path used during extraction.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// UTC ISO-8601 timestamp indicating when extraction completed.
    /// </summary>
    public string ExtractedAt { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>
    /// Deterministic source hash used by incremental workflows.
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>
    /// Target framework moniker parsed from the main project file.
    /// </summary>
    public string TargetFramework { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable ORM label (for example XPO or EF Core).
    /// </summary>
    public string OrmType { get; set; } = "XPO";

    /// <summary>
    /// Package references detected in the project file.
    /// </summary>
    public List<string> PackageReferences { get; set; } = [];

    /// <summary>
    /// Extracted business entities.
    /// </summary>
    public List<ExtractedEntity> Entities { get; set; } = [];

    /// <summary>
    /// Extracted XAF controllers.
    /// </summary>
    public List<ExtractedController> Controllers { get; set; } = [];

    /// <summary>
    /// Navigation groups derived from entity metadata.
    /// </summary>
    public List<ExtractedNavigationItem> Navigation { get; set; } = [];

    /// <summary>
    /// Seed data blocks extracted from updater classes.
    /// </summary>
    public List<ExtractedSeedData> SeedData { get; set; } = [];

    /// <summary>
    /// Module metadata when a module class is found.
    /// </summary>
    public ExtractedModuleInfo? ModuleInfo { get; set; }

    /// <summary>
    /// Optional model editor customization data extracted from xafml files.
    /// </summary>
    public ExtractedModelInfo? ModelEditorInfo { get; set; }

    /// <summary>
    /// Property and list editors this team wrote, usually living in a platform project beside the
    /// module rather than in it.
    /// </summary>
    public List<ExtractedEditor> Editors { get; set; } = [];

    /// <summary>
    /// DevExpress version of the ground-truth catalog that informed this extraction, or null when
    /// none was available.
    /// </summary>
    public string? CatalogVersion { get; set; }

    /// <summary>
    /// Attributes used in this application that XAF does not define, and that are not part of
    /// .NET either — so they are the team's own. Populated only when a catalog is present.
    /// </summary>
    /// <remarks>
    /// Worth surfacing: a custom attribute usually marks a convention this team invented, which
    /// cannot be looked up in any documentation.
    /// </remarks>
    public List<string> CustomAttributes { get; set; } = [];
}

/// <summary>
/// Metadata extracted from the main XAF module class.
/// </summary>
public class ExtractedModuleInfo
{
    /// <summary>
    /// Module class name (usually derived from ModuleBase).
    /// </summary>
    public string ModuleClassName { get; set; } = string.Empty;

    /// <summary>
    /// Types explicitly exported/registered by the module.
    /// </summary>
    public List<string> RegisteredTypes { get; set; } = [];

    /// <summary>
    /// Required modules referenced by the module configuration.
    /// </summary>
    public List<string> RequiredModules { get; set; } = [];
}
