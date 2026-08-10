using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// Provides controller and action extraction capabilities for XAF controller source code.
/// </summary>
public interface IControllerAnalyzer
{
    /// <summary>
    /// Analyzes all matching controller files in a source directory.
    /// </summary>
    /// <param name="sourceDirectory">Root directory to search.</param>
    /// <param name="options">Extraction behavior and filters.</param>
    /// <returns>Extracted controllers and action metadata.</returns>
    List<ExtractedController> AnalyzeControllers(string sourceDirectory, ExtractionOptions options);

    /// <summary>
    /// Analyzes a single controller file.
    /// </summary>
    /// <param name="filePath">Path to the C# file to parse.</param>
    /// <param name="options">Extraction behavior and filters.</param>
    /// <returns>The extracted controller or null when the file is not a XAF controller.</returns>
    ExtractedController? AnalyzeControllerFile(string filePath, ExtractionOptions options);
}
