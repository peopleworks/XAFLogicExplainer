using System.Diagnostics;
using XafLogicExplainer.CopilotSync.Models;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Hashing;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.CopilotSync.Services;

/// <summary>
/// Coordinates incremental extraction and synchronization with PeopleWorks Copilot.
/// </summary>
public class IncrementalSyncService
{
    private readonly ILogicExtractor _extractor;
    private readonly IChangeDetector _changeDetector;

    /// <summary>
    /// Creates a sync service with default extractor and hash calculator implementations.
    /// </summary>
    public IncrementalSyncService()
    {
        _extractor = new LogicExtractor();
        _changeDetector = new ProjectHashCalculator();
    }

    /// <summary>
    /// Creates a sync service with explicit dependencies.
    /// </summary>
    /// <param name="extractor">Project extraction service.</param>
    /// <param name="changeDetector">Change detection service.</param>
    public IncrementalSyncService(ILogicExtractor extractor, IChangeDetector changeDetector)
    {
        _extractor = extractor;
        _changeDetector = changeDetector;
    }

    /// <summary>
    /// Full sync pipeline: detect changes, extract, generate docs, upload to Copilot.
    /// </summary>
    public async Task<SyncResult> SyncAsync(SyncConfiguration config, ExtractionOptions? extractionOptions = null, Action<string>? onProgress = null, Func<ExtractedProject, Task>? enrichmentHook = null)
    {
        var sw = Stopwatch.StartNew();
        onProgress?.Invoke($"Starting sync for project: {config.ProjectPath}");

        // 1. Check for changes (skip if ForceSync)
        if (!config.ForceSync)
        {
            var hasChanged = _changeDetector.HasChanged(config.ProjectPath);
            if (!hasChanged)
            {
                onProgress?.Invoke("No changes detected. Skipping sync.");
                return new SyncResult
                {
                    Success = true,
                    Message = "No changes detected. Documentation is up to date.",
                    Duration = sw.Elapsed
                };
            }
            onProgress?.Invoke("Changes detected. Proceeding with extraction...");
        }
        else
        {
            onProgress?.Invoke("Force sync enabled. Skipping change detection.");
        }

        // 2. Extract project logic
        onProgress?.Invoke("Extracting project logic...");
        extractionOptions ??= new ExtractionOptions
        {
            BaseTypeNames = ["XPCustomObject", "BaseObject", "XPObject", "XPLiteObject", "PermissionPolicyUser"],
            IncludeSourceCode = true,
            IncludeMethodBodies = true,
            IncludeComments = true,
            LanguageCode = config.LanguageCode
        };

        ExtractedProject project;
        try
        {
            project = _extractor.ExtractFromSourceDirectory(config.ProjectPath, extractionOptions);
            onProgress?.Invoke($"Extracted: {project.Entities.Count} entities, {project.Controllers.Count} controllers");
        }
        catch (Exception ex)
        {
            return new SyncResult
            {
                Success = false,
                Message = $"Extraction failed: {ex.Message}",
                Errors = [ex.Message],
                Duration = sw.Elapsed
            };
        }

        // 2.5. Optional AI enrichment
        if (enrichmentHook != null)
        {
            onProgress?.Invoke("Enriching controllers with AI business logic summaries...");
            await enrichmentHook(project);
        }

        // 3. Upload documentation to Copilot
        onProgress?.Invoke("Uploading documentation to PeopleWorks Copilot...");
        SyncResult result;

        using (var apiClient = new CopilotApiClient(
                   config.CopilotApiBaseUrl,
                   config.CopilotApiToken,
                   config.UserName,
                   config.ResourceName))
        {
            var uploader = new DocumentationUploader(apiClient, languageCode: extractionOptions.LanguageCode);
            result = await uploader.UploadProjectDocumentationAsync(project, onProgress);
        }

        // 4. Save hash if successful
        if (result.Success)
        {
            _changeDetector.SaveHash(config.ProjectPath, project.SourceHash);
            onProgress?.Invoke($"Hash saved. Next sync will only run if files change.");
        }

        result.Duration = sw.Elapsed;
        onProgress?.Invoke($"Sync completed in {result.Duration.TotalSeconds:F1}s: {result.Message}");

        return result;
    }

    /// <summary>
    /// Extract and upload without change detection. Always runs.
    /// </summary>
    public async Task<SyncResult> ForceSyncAsync(SyncConfiguration config, ExtractionOptions? extractionOptions = null, Action<string>? onProgress = null)
    {
        config.ForceSync = true;
        return await SyncAsync(config, extractionOptions, onProgress);
    }

    /// <summary>
    /// Extract only (no upload). Useful for preview/testing.
    /// </summary>
    public ExtractedProject ExtractOnly(string projectPath, ExtractionOptions? options = null)
    {
        return _extractor.ExtractFromSourceDirectory(projectPath, options);
    }

    /// <summary>
    /// Test connectivity to PeopleWorks Copilot API.
    /// </summary>
    public async Task<(bool connected, string message)> TestConnectionAsync(SyncConfiguration config)
    {
        try
        {
            using var apiClient = new CopilotApiClient(
                config.CopilotApiBaseUrl,
                config.CopilotApiToken,
                config.UserName,
                config.ResourceName);

            var docs = await apiClient.ListDocumentsAsync();
            if (docs.Success)
                return (true, $"Connected. {docs.Count} existing documents found.");

            return (false, $"API returned error: {docs.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Connection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get AI provider info from PeopleWorks Copilot (model, API key).
    /// </summary>
    public async Task<AiProviderInfo?> GetAiProviderAsync(SyncConfiguration config)
    {
        using var apiClient = new CopilotApiClient(
            config.CopilotApiBaseUrl,
            config.CopilotApiToken,
            config.UserName,
            config.ResourceName);

        return await apiClient.GetAiProviderAsync();
    }
}
