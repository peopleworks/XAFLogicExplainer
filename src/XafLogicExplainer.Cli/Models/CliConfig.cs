namespace XafLogicExplainer.Cli.Models;

/// <summary>
/// Persisted CLI defaults and multi-project configuration values.
/// </summary>
public class CliConfig
{
    /// <summary>
    /// Default Copilot API base URL.
    /// </summary>
    public string? ApiUrl { get; set; }

    /// <summary>
    /// Default API token.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Default user name for Copilot API requests.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Default Copilot resource name.
    /// </summary>
    public string? ResourceName { get; set; }

    /// <summary>
    /// Default local project path.
    /// </summary>
    public string? ProjectPath { get; set; }

    /// <summary>
    /// Default documentation language code.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Default ORM mode.
    /// </summary>
    public string? Orm { get; set; }

    /// <summary>
    /// Named project profiles for batch processing.
    /// </summary>
    public List<ProjectConfig> Projects { get; set; } = [];
}
