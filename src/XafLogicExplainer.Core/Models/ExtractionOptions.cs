using System.Text.Json.Serialization;

namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Supported ORM strategies for extraction behavior and relationship inference.
/// </summary>
public enum OrmType
{
    /// <summary>
    /// Detect ORM automatically from source patterns.
    /// </summary>
    Auto,
    /// <summary>
    /// Treat project as XPO-based.
    /// </summary>
    Xpo,
    /// <summary>
    /// Treat project as Entity Framework Core-based.
    /// </summary>
    EfCore
}

/// <summary>
/// Controls source scanning scope and output richness during project extraction.
/// </summary>
public class ExtractionOptions
{
    /// <summary>
    /// Glob-like patterns for business object discovery.
    /// </summary>
    public string[] BusinessObjectPatterns { get; set; } = ["**/BusinessObjects/**/*.cs"];

    /// <summary>
    /// Glob-like patterns for controller discovery.
    /// </summary>
    public string[] ControllerPatterns { get; set; } = ["**/Controllers/**/*.cs"];

    /// <summary>
    /// Optional path pattern for module file location.
    /// </summary>
    public string? ModuleFilePattern { get; set; } = "**/Module.cs";

    /// <summary>
    /// Optional path pattern for updater file location.
    /// </summary>
    public string? UpdaterFilePattern { get; set; } = "**/Updater.cs";

    /// <summary>
    /// Exclusion patterns (for example bin/obj directories).
    /// </summary>
    public string[] ExcludePatterns { get; set; } = ["**/obj/**", "**/bin/**"];

    /// <summary>
    /// Known business object base types used to classify entity classes.
    /// </summary>
    public string[] BaseTypeNames { get; set; } = [
        "XPCustomObject",
        "BaseObject",
        "XPObject",
        "XPLiteObject"
    ];

    /// <summary>
    /// Known XAF controller base types used to classify controller classes.
    /// </summary>
    public string[] ControllerBaseTypeNames { get; set; } = [
        "ViewController",
        "ObjectViewController",
        "WindowController"
    ];

    /// <summary>
    /// Includes raw code snippets in extracted output when true.
    /// </summary>
    public bool IncludeSourceCode { get; set; } = true;

    /// <summary>
    /// Includes method body text for controller and updater methods when true.
    /// </summary>
    public bool IncludeMethodBodies { get; set; } = true;

    /// <summary>
    /// Includes source comments collected from syntax trivia when true.
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// Output language code used by generators and sync pipeline.
    /// </summary>
    public string LanguageCode { get; set; } = "es";

    /// <summary>
    /// Enables extraction of Model Editor information from xafml files.
    /// </summary>
    public bool IncludeModelEditor { get; set; } = true;

    /// <summary>
    /// Enables sibling platform project discovery for xafml files.
    /// </summary>
    public bool DiscoverPlatformModels { get; set; } = true;

    /// <summary>
    /// User-configured ORM mode. <see cref="OrmType.Auto"/> triggers runtime detection.
    /// </summary>
    public OrmType Orm { get; set; } = OrmType.Auto;

    /// <summary>
    /// Actual ORM resolved by analyzers after auto-detection.
    /// </summary>
    [JsonIgnore]
    public OrmType ResolvedOrm { get; set; } = OrmType.Xpo;

    /// <summary>
    /// Whether to load the locally generated DevExpress ground-truth catalog, when one exists.
    /// </summary>
    /// <remarks>
    /// On by default so that anyone who has run <c>xaflogic catalog build</c> benefits without
    /// asking. Turn it off for reproducible output that must not vary with what happens to be
    /// installed on the machine.
    /// </remarks>
    public bool UseCatalog { get; set; } = true;

    /// <summary>
    /// An explicit catalog, overriding the one that would be loaded from disk.
    /// </summary>
    /// <remarks>
    /// Written with the namespace spelled out. The property is called <c>Catalog</c>, so inside
    /// this class the simple name binds to the member rather than to the namespace of the same
    /// name, and <c>Catalog.XafCatalog</c> does not compile.
    /// </remarks>
    [JsonIgnore]
    public XafLogicExplainer.Core.Catalog.XafCatalog? Catalog { get; set; }
}
