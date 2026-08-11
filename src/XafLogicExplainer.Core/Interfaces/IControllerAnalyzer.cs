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
    /// <remarks>
    /// Returns every controller the file declares. One file holding several is ordinary C# — small
    /// controllers are routinely grouped — and returning only the first silently dropped the rest.
    /// </remarks>
    /// <param name="filePath">Path to the C# file to parse.</param>
    /// <param name="options">Extraction behavior and filters.</param>
    /// <returns>The controllers declared in the file, empty when it declares none.</returns>
    List<ExtractedController> AnalyzeControllerFile(string filePath, ExtractionOptions options);
}
