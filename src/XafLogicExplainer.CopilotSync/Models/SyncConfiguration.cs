namespace XafLogicExplainer.CopilotSync.Models;

/// <summary>
/// Runtime configuration required to extract and synchronize documentation with Copilot.
/// </summary>
public class SyncConfiguration
{
    /// <summary>
    /// Copilot API base URL.
    /// </summary>
    public string CopilotApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// API token used as request header for Copilot endpoints.
    /// </summary>
    public string CopilotApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Copilot resource/knowledge-base name.
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// User identity sent to Copilot APIs.
    /// </summary>
    public string UserName { get; set; } = "xaf-logic-explainer";

    /// <summary>
    /// Path to the XAF project/module to process.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Language code used by generated documentation content.
    /// </summary>
    public string LanguageCode { get; set; } = "es";

    /// <summary>
    /// Forces sync even when no source changes are detected.
    /// </summary>
    public bool ForceSync { get; set; } = false;
}

/// <summary>
/// Final outcome payload produced by synchronization workflows.
/// </summary>
public class SyncResult
{
    /// <summary>
    /// Indicates whether synchronization completed without failures.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable operation summary.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Number of documents successfully uploaded.
    /// </summary>
    public int DocumentsUploaded { get; set; }

    /// <summary>
    /// Number of documents that failed during upload.
    /// </summary>
    public int DocumentsFailed { get; set; }

    /// <summary>
    /// Per-document upload details.
    /// </summary>
    public List<UploadedDocument> UploadedDocuments { get; set; } = [];

    /// <summary>
    /// Error messages collected during sync.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Source hash associated with this synchronization cycle.
    /// </summary>
    public string ProjectHash { get; set; } = string.Empty;

    /// <summary>
    /// Total elapsed duration.
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Per-document upload status details.
/// </summary>
public class UploadedDocument
{
    /// <summary>
    /// Internal uploaded document file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Document title shown to users.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Content length sent to API.
    /// </summary>
    public int ContentLength { get; set; }

    /// <summary>
    /// Indicates whether upload succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message when upload failed.
    /// </summary>
    public string? Error { get; set; }
}
