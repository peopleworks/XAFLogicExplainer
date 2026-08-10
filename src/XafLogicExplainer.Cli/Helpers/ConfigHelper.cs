using System.Text.Json;
using XafLogicExplainer.Cli.Models;

namespace XafLogicExplainer.Cli.Helpers;

/// <summary>
/// Handles persistence and retrieval of CLI configuration from the user profile.
/// </summary>
public static class ConfigHelper
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".xaflogic",
        "config.json"
    );

    /// <summary>
    /// Loads CLI configuration from disk.
    /// </summary>
    /// <returns>Deserialized configuration or defaults when file is absent/corrupt.</returns>
    public static CliConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new CliConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<CliConfig>(json) ?? new CliConfig();
        }
        catch
        {
            return new CliConfig();
        }
    }

    /// <summary>
    /// Saves configuration to disk, creating parent directory when needed.
    /// </summary>
    /// <param name="config">Configuration payload to persist.</param>
    public static void Save(CliConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir!);

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Removes stored configuration file.
    /// </summary>
    public static void Clear()
    {
        if (File.Exists(ConfigPath))
            File.Delete(ConfigPath);
    }

    /// <summary>
    /// Gets the full path of the configuration file.
    /// </summary>
    public static string GetConfigPath() => ConfigPath;
}
