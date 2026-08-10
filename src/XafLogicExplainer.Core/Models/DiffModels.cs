namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Aggregated change report produced by comparing two extracted project snapshots.
/// </summary>
public class ProjectDiffReport
{
    /// <summary>
    /// Project name from the current snapshot.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Hash from the previous extraction.
    /// </summary>
    public string PreviousHash { get; set; } = string.Empty;

    /// <summary>
    /// Hash from the current extraction.
    /// </summary>
    public string CurrentHash { get; set; } = string.Empty;

    /// <summary>
    /// Previous extraction timestamp.
    /// </summary>
    public string PreviousExtractedAt { get; set; } = string.Empty;

    /// <summary>
    /// Current extraction timestamp.
    /// </summary>
    public string CurrentExtractedAt { get; set; } = string.Empty;

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public string GeneratedAt { get; set; } = DateTime.UtcNow.ToString("o");

    /// <summary>
    /// High-level counters grouped by domain area.
    /// </summary>
    public DiffSummary Summary { get; set; } = new();

    /// <summary>
    /// Entity-level change collection.
    /// </summary>
    public CollectionDiff<EntityDiff> EntityChanges { get; set; } = new();

    /// <summary>
    /// Controller-level change collection.
    /// </summary>
    public CollectionDiff<ControllerDiff> ControllerChanges { get; set; } = new();

    /// <summary>
    /// Navigation-level change collection.
    /// </summary>
    public CollectionDiff<NavigationDiff> NavigationChanges { get; set; } = new();

    /// <summary>
    /// Seed data change collection.
    /// </summary>
    public CollectionDiff<string> SeedDataChanges { get; set; } = new();

    /// <summary>
    /// Optional model editor differences.
    /// </summary>
    public ModelEditorDiff? ModelEditorChanges { get; set; }

    /// <summary>
    /// Indicates whether any meaningful differences were detected.
    /// </summary>
    public bool HasChanges => Summary.HasChanges;
}

/// <summary>
/// Totalized counters for a diff report.
/// </summary>
public class DiffSummary
{
    public int TotalAdded { get; set; }
    public int TotalRemoved { get; set; }
    public int TotalModified { get; set; }
    public bool HasChanges => TotalAdded > 0 || TotalRemoved > 0 || TotalModified > 0;

    public int EntitiesAdded { get; set; }
    public int EntitiesRemoved { get; set; }
    public int EntitiesModified { get; set; }
    public int ControllersAdded { get; set; }
    public int ControllersRemoved { get; set; }
    public int ControllersModified { get; set; }
    public int NavigationAdded { get; set; }
    public int NavigationRemoved { get; set; }
    public int NavigationModified { get; set; }
    public int SeedDataAdded { get; set; }
    public int SeedDataRemoved { get; set; }
}

/// <summary>
/// Generic added/removed/modified collection diff structure.
/// </summary>
public class CollectionDiff<T>
{
    public List<string> Added { get; set; } = [];
    public List<string> Removed { get; set; } = [];
    public List<T> Modified { get; set; } = [];
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Modified.Count > 0;
}

/// <summary>
/// Entity-level diff payload.
/// </summary>
public class EntityDiff
{
    public string ClassName { get; set; } = string.Empty;
    public List<string> AddedProperties { get; set; } = [];
    public List<string> RemovedProperties { get; set; } = [];
    public List<PropertyDiff> ModifiedProperties { get; set; } = [];
    public List<string> AddedRelationships { get; set; } = [];
    public List<string> RemovedRelationships { get; set; } = [];
    public List<string> AddedValidationRules { get; set; } = [];
    public List<string> RemovedValidationRules { get; set; } = [];
    public List<string> AddedAppearanceRules { get; set; } = [];
    public List<string> RemovedAppearanceRules { get; set; } = [];
    public FieldChange? DescriptionChange { get; set; }
    public FieldChange? CaptionChange { get; set; }
    public FieldChange? BaseTypeChange { get; set; }

    public bool HasChanges =>
        AddedProperties.Count > 0 || RemovedProperties.Count > 0 || ModifiedProperties.Count > 0 ||
        AddedRelationships.Count > 0 || RemovedRelationships.Count > 0 ||
        AddedValidationRules.Count > 0 || RemovedValidationRules.Count > 0 ||
        AddedAppearanceRules.Count > 0 || RemovedAppearanceRules.Count > 0 ||
        DescriptionChange != null || CaptionChange != null || BaseTypeChange != null;
}

/// <summary>
/// Property-level diff payload.
/// </summary>
public class PropertyDiff
{
    public string PropertyName { get; set; } = string.Empty;
    public List<FieldChange> Changes { get; set; } = [];
}

/// <summary>
/// Represents one field transition from old to new value.
/// </summary>
public class FieldChange
{
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}

/// <summary>
/// Controller-level diff payload.
/// </summary>
public class ControllerDiff
{
    public string ClassName { get; set; } = string.Empty;
    public List<string> AddedActions { get; set; } = [];
    public List<string> RemovedActions { get; set; } = [];
    public List<ActionDiff> ModifiedActions { get; set; } = [];
    public FieldChange? TargetObjectTypeChange { get; set; }
    public FieldChange? TargetViewTypeChange { get; set; }

    public bool HasChanges =>
        AddedActions.Count > 0 || RemovedActions.Count > 0 || ModifiedActions.Count > 0 ||
        TargetObjectTypeChange != null || TargetViewTypeChange != null;
}

/// <summary>
/// Action-level diff payload.
/// </summary>
public class ActionDiff
{
    public string ActionId { get; set; } = string.Empty;
    public List<FieldChange> Changes { get; set; } = [];
}

/// <summary>
/// Navigation group diff payload.
/// </summary>
public class NavigationDiff
{
    public string GroupName { get; set; } = string.Empty;
    public List<string> AddedEntities { get; set; } = [];
    public List<string> RemovedEntities { get; set; } = [];
}

/// <summary>
/// Model editor diff payload.
/// </summary>
public class ModelEditorDiff
{
    public List<string> AddedViews { get; set; } = [];
    public List<string> RemovedViews { get; set; } = [];
    public List<string> ModifiedViews { get; set; } = [];
    public List<string> AddedBOClasses { get; set; } = [];
    public List<string> RemovedBOClasses { get; set; } = [];
    public List<string> ModifiedBOClasses { get; set; } = [];
    public bool HasChanges =>
        AddedViews.Count > 0 || RemovedViews.Count > 0 || ModifiedViews.Count > 0 ||
        AddedBOClasses.Count > 0 || RemovedBOClasses.Count > 0 || ModifiedBOClasses.Count > 0;
}
