using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// Generates end-user documentation artifacts from an extracted project snapshot.
/// </summary>
public interface IDocumentationGenerator
{
    /// <summary>
    /// Generates a single combined markdown document.
    /// </summary>
    /// <param name="project">Extracted project model.</param>
    /// <returns>Markdown text representing full documentation.</returns>
    string GenerateMarkdown(ExtractedProject project);

    /// <summary>
    /// Serializes the extracted project to JSON.
    /// </summary>
    /// <param name="project">Extracted project model.</param>
    /// <returns>JSON document.</returns>
    string GenerateJson(ExtractedProject project);

    /// <summary>
    /// Generates logical documentation sections that can be uploaded independently.
    /// </summary>
    /// <param name="project">Extracted project model.</param>
    /// <returns>A set of section descriptors and contents.</returns>
    List<DocumentSection> GenerateSections(ExtractedProject project);
}

/// <summary>
/// Represents one standalone documentation fragment generated from project metadata.
/// </summary>
public class DocumentSection
{
    /// <summary>
    /// Human-readable section title shown in clients and reports.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stable file/document identifier used for output naming.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Main markdown body for this section.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Short descriptive text used by remote indexing systems.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated labels for search/filtering in target systems.
    /// </summary>
    public string Tags { get; set; } = string.Empty;
}
