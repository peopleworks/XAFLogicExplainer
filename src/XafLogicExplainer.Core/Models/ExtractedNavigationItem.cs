namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Represents one logical navigation group and the entities assigned to it.
/// </summary>
public class ExtractedNavigationItem
{
    /// <summary>
    /// Navigation group caption/name.
    /// </summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>
    /// Entity class names linked to this group.
    /// </summary>
    public List<string> EntityClassNames { get; set; } = [];
}
