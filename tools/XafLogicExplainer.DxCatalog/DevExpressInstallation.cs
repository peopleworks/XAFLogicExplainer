using System.Text.RegularExpressions;

namespace XafLogicExplainer.DxCatalog;

/// <summary>
/// A DevExpress installation on this machine.
/// </summary>
public sealed partial class DevExpressInstallation
{
    /// <summary>Version, e.g. "26.1".</summary>
    public required string Version { get; init; }

    /// <summary>Directory holding the .NET assemblies.</summary>
    public required string AssemblyDirectory { get; init; }

    /// <summary>XAF assemblies found there.</summary>
    public required IReadOnlyList<string> XafAssemblies { get; init; }

    /// <summary>
    /// Finds an installation, preferring an explicit path.
    /// </summary>
    /// <param name="explicitPath">
    /// A DevExpress root, or a directory of assemblies. Null searches the usual locations.
    /// </param>
    public static DevExpressInstallation? Locate(string? explicitPath = null)
    {
        foreach (var directory in CandidateDirectories(explicitPath))
        {
            if (!Directory.Exists(directory))
                continue;

            var assemblies = Directory
                .EnumerateFiles(directory, "DevExpress.*.dll")
                .Where(IsXafAssembly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (assemblies.Count == 0)
                continue;

            return new DevExpressInstallation
            {
                Version = InferVersion(assemblies[0], directory),
                AssemblyDirectory = directory,
                XafAssemblies = assemblies,
            };
        }

        return null;
    }

    /// <summary>
    /// Produces the directories worth looking in, most specific first.
    /// </summary>
    private static IEnumerable<string> CandidateDirectories(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            // The path may already be the assembly directory, or the installation root.
            yield return explicitPath;
            yield return Path.Combine(explicitPath, "Components", "Bin", "NetCore");
            yield return Path.Combine(explicitPath, "Bin", "NetCore");
            yield break;
        }

        // Newest first, so a machine with several versions installed gets the current one.
        foreach (var programFiles in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            if (string.IsNullOrWhiteSpace(programFiles) || !Directory.Exists(programFiles))
                continue;

            var roots = Directory
                .EnumerateDirectories(programFiles, "DevExpress *")
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
                yield return Path.Combine(root, "Components", "Bin", "NetCore");
        }
    }

    /// <summary>
    /// Whether an assembly is part of XAF or its persistence layer.
    /// </summary>
    /// <remarks>
    /// Restricted on purpose. A DevExpress installation contains hundreds of assemblies for grids,
    /// charts and reporting; reading them all would take far longer and produce a catalog full of
    /// types that say nothing about an XAF application's structure.
    /// <para>
    /// <c>DevExpress.Xpo</c> is included because an XAF business class is written in its
    /// vocabulary — <c>[Size]</c>, <c>[Indexed]</c>, <c>[Aggregated]</c>, <c>[PersistentAlias]</c>
    /// all live there. Leaving it out made the catalog report those as attributes the application
    /// had invented, which is worse than not classifying them at all.
    /// </para>
    /// </remarks>
    private static bool IsXafAssembly(string path)
    {
        var name = Path.GetFileName(path);

        return name.StartsWith("DevExpress.ExpressApp", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DevExpress.Persistent", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("DevExpress.Xpo", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the version from an assembly file name, falling back to the installation directory.
    /// </summary>
    private static string InferVersion(string assemblyPath, string directory)
    {
        // DevExpress.ExpressApp.v26.1.dll
        var fromAssembly = VersionInFileName().Match(Path.GetFileName(assemblyPath));
        if (fromAssembly.Success)
            return fromAssembly.Groups[1].Value;

        // ...\DevExpress 26.1\Components\Bin\NetCore
        var fromDirectory = VersionInDirectory().Match(directory);
        return fromDirectory.Success ? fromDirectory.Groups[1].Value : "unknown";
    }

    [GeneratedRegex(@"\.v(\d+\.\d+)\.dll$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionInFileName();

    [GeneratedRegex(@"DevExpress[ _](\d+\.\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionInDirectory();
}
