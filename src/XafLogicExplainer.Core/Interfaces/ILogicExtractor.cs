using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// Defines the orchestration contract that extracts a complete logical model from a XAF source project.
/// </summary>
public interface ILogicExtractor
{
    /// <summary>
    /// Scans the target project directory and returns an aggregated extraction result.
    /// </summary>
    /// <param name="projectPath">Absolute or relative path to the XAF module directory.</param>
    /// <param name="options">Optional extraction settings; defaults are used when null.</param>
    /// <returns>A fully populated <see cref="ExtractedProject"/> snapshot.</returns>
    ExtractedProject ExtractFromSourceDirectory(string projectPath, ExtractionOptions? options = null);
}
