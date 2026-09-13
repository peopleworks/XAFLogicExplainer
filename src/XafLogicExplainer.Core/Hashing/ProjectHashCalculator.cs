using System.Security.Cryptography;
using System.Text;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Interfaces;

namespace XafLogicExplainer.Core.Hashing;

/// <summary>
/// Computes and persists deterministic project hashes for incremental workflows.
/// </summary>
public class ProjectHashCalculator : IChangeDetector
{
    private const string HashFileName = ".xaflogicexplainer";

    /// <summary>
    /// Computes a SHA-256 hash over every file the extraction can read.
    /// </summary>
    /// <remarks>
    /// It has to cover everything the extraction reads, or the documentation goes stale while the
    /// tool reports it fresh -- which is worse than no change detection at all, because nobody
    /// re-runs a command that just said there was nothing to do.
    /// <para>
    /// This hash used to list those files itself, and each time the extraction learned to read
    /// somewhere new the list fell behind: referenced source first, then the controllers in a
    /// Blazor.Server project and the report layouts at the solution root, which it never covered.
    /// The list now comes from <see cref="SourceRoster"/>, which is built from the extraction's own
    /// discovery, and the MCP server's cache fingerprint walks the same one.
    /// </para>
    /// <para>
    /// Each file's path goes in with its bytes, so a file that moves changes the hash as well: the
    /// documentation says where things are declared.
    /// </para>
    /// </remarks>
    /// <param name="projectDirectory">Root project directory.</param>
    /// <returns>Upper-case hexadecimal hash string.</returns>
    public string ComputeHash(string projectDirectory)
    {
        using var sha256 = SHA256.Create();
        foreach (var file in SourceRoster.Files(projectDirectory))
        {
            var name = Encoding.UTF8.GetBytes(Path.GetRelativePath(projectDirectory, file) + "\n");
            sha256.TransformBlock(name, 0, name.Length, null, 0);

            byte[] content;
            try
            {
                content = File.ReadAllBytes(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Locked or gone since the roster was listed. Its path is still in the hash.
                continue;
            }

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
