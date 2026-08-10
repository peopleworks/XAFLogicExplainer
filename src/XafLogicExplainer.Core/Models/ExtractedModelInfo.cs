namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Root result from parsing one or more xafml files.
/// </summary>
public class ExtractedModelInfo
{
    /// <summary>
    /// Optional application title from model metadata.
    /// </summary>
    public string? ApplicationTitle { get; set; }

    /// <summary>
    /// BOModel class-level customizations.
    /// </summary>
    public List<ModelClassInfo> BOModelClasses { get; set; } = [];

    /// <summary>
    /// View definitions and customizations.
    /// </summary>
    public List<ModelViewInfo> Views { get; set; } = [];

    /// <summary>
    /// Navigation customizations.
    /// </summary>
    public ModelNavigationInfo? NavigationItems { get; set; }

    /// <summary>
    /// Application-wide UI options extracted from model metadata.
    /// </summary>
    public ModelOptionsInfo? Options { get; set; }

    /// <summary>
    /// Registered schema modules.
    /// </summary>
    public List<ModelSchemaModuleInfo> SchemaModules { get; set; } = [];

    /// <summary>
    /// Physical source files that contributed to this merged model snapshot.
    /// </summary>
    public List<string> SourceFiles { get; set; } = [];
}

/// <summary>
/// Describes BOModel customization for one business class.
/// </summary>
public class ModelClassInfo
{
    /// <summary>
    /// Fully-qualified class name.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Short class name derived from <see cref="FullName"/>.
    /// </summary>
    public string ClassName => FullName.Contains('.') ? FullName[(FullName.LastIndexOf('.') + 1)..] : FullName;

    /// <summary>
    /// Caption override.
    /// </summary>
    public string? Caption { get; set; }

    /// <summary>
    /// Indicates clone support at model layer.
    /// </summary>
    public bool IsCloneable { get; set; }

    /// <summary>
    /// Additional unmodeled attributes retained as key/value pairs.
    /// </summary>
    public Dictionary<string, string> CustomAttributes { get; set; } = [];
}

/// <summary>
/// Supported XAF model view categories.
/// </summary>
public enum ModelViewType
{
    /// <summary>
    /// List view definition.
    /// </summary>
    ListView,
    /// <summary>
    /// Detail view definition.
    /// </summary>
    DetailView,
    /// <summary>
    /// Dashboard view definition.
    /// </summary>
    DashboardView
}

/// <summary>
/// Describes one model view and its configured behavior.
/// </summary>
public class ModelViewInfo
{
    /// <summary>
    /// Stable model view identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// View category.
    /// </summary>
    public ModelViewType ViewType { get; set; }

    /// <summary>
    /// Caption override.
    /// </summary>
    public string? Caption { get; set; }
    public bool? AllowEdit { get; set; }
    public bool? AllowDelete { get; set; }
    public bool? AllowNew { get; set; }
    public string? Criteria { get; set; }
    public string? EditorTypeName { get; set; }
    public List<ModelColumnInfo> Columns { get; set; } = [];
    public List<ModelFilterInfo> Filters { get; set; } = [];
    public string? CurrentFilterId { get; set; }
}

/// <summary>
/// Describes one configured list-view column in model metadata.
/// </summary>
public class ModelColumnInfo
{
    public string Id { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
    public int? Index { get; set; }
    public string? SortOrder { get; set; }
    public int? Width { get; set; }
    public string? Caption { get; set; }
}

/// <summary>
/// Describes one predefined model filter.
/// </summary>
public class ModelFilterInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? Criteria { get; set; }
}

/// <summary>
/// Root navigation metadata from Model Editor.
/// </summary>
public class ModelNavigationInfo
{
    public string? NavigationStyle { get; set; }
    public string? StartupNavigationItem { get; set; }
    public List<ModelNavigationGroup> Groups { get; set; } = [];
}

/// <summary>
/// Navigation group node in Model Editor metadata.
/// </summary>
public class ModelNavigationGroup
{
    public string Id { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? ImageName { get; set; }
    public int? Index { get; set; }
    public List<ModelNavigationItemEntry> Items { get; set; } = [];
}

/// <summary>
/// Navigation item entry in Model Editor metadata.
/// </summary>
public class ModelNavigationItemEntry
{
    public string Id { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public string? ViewId { get; set; }
    public string? ImageName { get; set; }
    public int? Index { get; set; }
}

/// <summary>
/// Application-level options configured in Model Editor.
/// </summary>
public class ModelOptionsInfo
{
    public string? UIType { get; set; }
    public string? FormStyle { get; set; }
    public string? Skin { get; set; }
    public string? RequiredFieldMark { get; set; }
}

/// <summary>
/// Module metadata declared under <c>SchemaModules</c> in xafml.
/// </summary>
public class ModelSchemaModuleInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
}
