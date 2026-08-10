using System.Security.Cryptography;
using XafLogicExplainer.Core.Interfaces;

namespace XafLogicExplainer.Core.Hashing;

/// <summary>
/// Computes and persists deterministic project hashes for incremental workflows.
/// </summary>
public class ProjectHashCalculator : IChangeDetector
{
    private const string HashFileName = ".xaflogicexplainer";

    /// <summary>
    /// Computes a SHA-256 hash from relevant source files and model files.
    /// </summary>
    /// <param name="projectDirectory">Root project directory.</param>
    /// <returns>Upper-case hexadecimal hash string.</returns>
    public string ComputeHash(string projectDirectory)
    {
        var csFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories);
        var xafmlFiles = Directory.GetFiles(projectDirectory, "*.xafml", SearchOption.AllDirectories);

        // Also include sibling platform xafml files
        var parentDir = Directory.GetParent(projectDirectory)?.FullName;
        var siblingXafml = parentDir != null
            ? Directory.GetDirectories(parentDir)
                .Where(d => !d.Equals(projectDirectory, StringComparison.OrdinalIgnoreCase))
                .SelectMany(d =>
                {
                    // HACK: IO exceptions are intentionally swallowed to keep hashing resilient when sibling
                    // modules are inaccessible. A richer diagnostic channel is a future improvement.
                    try { return Directory.GetFiles(d, "*.xafml", SearchOption.TopDirectoryOnly); }
                    catch { return []; }
                })
            : Enumerable.Empty<string>();

        var files = csFiles.Concat(xafmlFiles).Concat(siblingXafml)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .OrderBy(f => f)
            .ToArray();

        using var sha256 = SHA256.Create();
        foreach (var file in files)
        {
            var content = File.ReadAllBytes(file);
            sha256.TransformBlock(content, 0, content.Length, null, 0);
        }
        sha256.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha256.Hash!);
    }

    /// <summary>
    /// Checks whether current hash differs from the previously saved value.
    /// </summary>
    public bool HasChanged(string projectDirectory)
    {
        var currentHash = ComputeHash(projectDirectory);
        var savedHash = LoadSavedHash(projectDirectory);
        return savedHash == null || savedHash != currentHash;
    }

    /// <summary>
    /// Computes and returns the current project hash.
    /// </summary>
    public string GetCurrentHash(string projectDirectory) => ComputeHash(projectDirectory);

    /// <summary>
    /// Returns the saved hash value when available.
    /// </summary>
    public string? GetSavedHash(string projectDirectory) => LoadSavedHash(projectDirectory);

    /// <summary>
    /// Persists a hash value in the project root.
    /// </summary>
    public void SaveHash(string projectDirectory, string hash)
    {
        var hashFile = Path.Combine(projectDirectory, HashFileName);
        File.WriteAllText(hashFile, hash);
    }

    /// <summary>
    /// Loads the persisted project hash if present.
    /// </summary>
    private static string? LoadSavedHash(string projectDirectory)
    {
        var hashFile = Path.Combine(projectDirectory, HashFileName);
        return File.Exists(hashFile) ? File.ReadAllText(hashFile).Trim() : null;
    }
}
