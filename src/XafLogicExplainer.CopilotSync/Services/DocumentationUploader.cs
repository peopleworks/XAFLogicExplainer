using XafLogicExplainer.CopilotSync.Models;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.CopilotSync.Services;

/// <summary>
/// Generates documentation sections and uploads them to PeopleWorks Copilot.
/// </summary>
public class DocumentationUploader
{
    private readonly CopilotApiClient _apiClient;
    private readonly IDocumentationGenerator _docGenerator;
    private readonly DocumentationLabels _labels;

    /// <summary>
    /// Creates a documentation uploader.
    /// </summary>
    /// <param name="apiClient">Configured Copilot API client.</param>
    /// <param name="docGenerator">Optional custom documentation generator.</param>
    /// <param name="languageCode">Language code used for labels and fallback generator.</param>
    public DocumentationUploader(CopilotApiClient apiClient, IDocumentationGenerator? docGenerator = null, string languageCode = "es")
    {
        _apiClient = apiClient;
        _docGenerator = docGenerator ?? new MarkdownDocumentationGenerator(languageCode);
        _labels = DocumentationLabels.ForLanguage(languageCode);
    }

    /// <summary>
    /// Generate documentation sections from an extracted project and upload each to PeopleWorks Copilot.
    /// </summary>
    public async Task<SyncResult> UploadProjectDocumentationAsync(ExtractedProject project, Action<string>? onProgress = null)
    {
        var result = new SyncResult
        {
            ProjectHash = project.SourceHash
        };

        var sections = _docGenerator.GenerateSections(project);

        onProgress?.Invoke($"Generated {sections.Count} documentation sections for {project.ProjectName}");

        foreach (var section in sections)
        {
            onProgress?.Invoke($"  Uploading: {section.Title} ({section.Content.Length:N0} chars)...");

            try
            {
                var response = await _apiClient.UploadLongTextAsync(
                    docName: section.FileName,
                    originalDocName: section.Title,
                    description: section.Description,
                    tags: $"auto-generated,xaf-logic-explainer,{section.Tags}",
                    textContent: section.Content
                );

                var uploaded = new UploadedDocument
                {
                    FileName = section.FileName,
                    Title = section.Title,
                    ContentLength = section.Content.Length,
                    Success = response.Success,
                    Error = response.Success ? null : response.Message
                };

                result.UploadedDocuments.Add(uploaded);

                if (response.Success)
                {
                    result.DocumentsUploaded++;
                    onProgress?.Invoke($"    OK: {section.Title}");
                }
                else
                {
                    result.DocumentsFailed++;
                    result.Errors.Add($"{section.Title}: {response.Message}");
                    onProgress?.Invoke($"    FAILED: {section.Title} - {response.Message}");
                }
            }
            catch (Exception ex)
            {
                result.DocumentsFailed++;
                result.Errors.Add($"{section.Title}: {ex.Message}");
                result.UploadedDocuments.Add(new UploadedDocument
                {
                    FileName = section.FileName,
                    Title = section.Title,
                    ContentLength = section.Content.Length,
                    Success = false,
                    Error = ex.Message
                });
                onProgress?.Invoke($"    ERROR: {section.Title} - {ex.Message}");
            }
        }

        // Also upload the full combined documentation
        onProgress?.Invoke("  Uploading: Full combined documentation...");
        try
        {
            var fullMarkdown = _docGenerator.GenerateMarkdown(project);
            var fullResponse = await _apiClient.UploadLongTextAsync(
                docName: $"{project.ProjectName}_Full",
                originalDocName: $"{project.ProjectName} - {_labels.FullDocumentation}",
                description: string.Format(_labels.FullDocDescription, project.ProjectName),
                tags: "auto-generated,xaf-logic-explainer,full-documentation",
                textContent: fullMarkdown
            );

            result.UploadedDocuments.Add(new UploadedDocument
            {
                FileName = $"{project.ProjectName}_Full",
                Title = _labels.FullDocumentation,
                ContentLength = fullMarkdown.Length,
                Success = fullResponse.Success,
                Error = fullResponse.Success ? null : fullResponse.Message
            });

            if (fullResponse.Success)
                result.DocumentsUploaded++;
            else
                result.DocumentsFailed++;
        }
        catch (Exception ex)
        {
            result.DocumentsFailed++;
            result.Errors.Add($"Full documentation: {ex.Message}");
        }

        result.Success = result.DocumentsFailed == 0;
        result.Message = result.Success
            ? $"Successfully uploaded {result.DocumentsUploaded} documents for {project.ProjectName}"
            : $"Uploaded {result.DocumentsUploaded} documents, {result.DocumentsFailed} failed";

        return result;
    }

    /// <summary>
    /// List existing documents in the Copilot resource to check what's already uploaded.
    /// </summary>
    public async Task<List<DocumentInfo>> GetExistingDocumentsAsync()
    {
        var response = await _apiClient.ListDocumentsAsync();
        return response.Documents ?? [];
    }
}
