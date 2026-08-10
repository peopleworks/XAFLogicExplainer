using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// Provides entity discovery and metadata extraction for XAF business objects.
/// </summary>
public interface IEntityAnalyzer
{
    /// <summary>
    /// Parses source files and returns business entities that match configured base type rules.
    /// </summary>
    /// <param name="sourceDirectory">Root directory to analyze.</param>
    /// <param name="options">Extraction behavior and filtering options.</param>
    /// <returns>A list of extracted entities with properties, relationships, and rules.</returns>
    List<ExtractedEntity> AnalyzeEntities(string sourceDirectory, ExtractionOptions options);
}
