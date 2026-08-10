namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// Computes and persists project fingerprints to enable incremental processing.
/// </summary>
public interface IChangeDetector
{
    /// <summary>
    /// Computes a deterministic hash for relevant source artifacts in the project.
    /// </summary>
    /// <param name="projectDirectory">Root project directory.</param>
    /// <returns>Hex-encoded hash.</returns>
    string ComputeHash(string projectDirectory);

    /// <summary>
    /// Determines whether current project contents differ from the last persisted hash.
    /// </summary>
    /// <param name="projectDirectory">Root project directory.</param>
    /// <returns>True when changes are detected or no prior hash exists.</returns>
    bool HasChanged(string projectDirectory);

    /// <summary>
    /// Persists a hash value for future change checks.
    /// </summary>
    /// <param name="projectDirectory">Root project directory.</param>
    /// <param name="hash">Hash value to store.</param>
    void SaveHash(string projectDirectory, string hash);
}
