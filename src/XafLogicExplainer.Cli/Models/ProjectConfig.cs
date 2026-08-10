namespace XafLogicExplainer.Cli.Models;

/// <summary>
/// Named project profile used by multi-project CLI commands.
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Friendly profile name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// XAF project root path.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Copilot resource name bound to this profile.
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// Optional language override for this profile.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional ORM override for this profile.
    /// </summary>
    public string? Orm { get; set; }
}
