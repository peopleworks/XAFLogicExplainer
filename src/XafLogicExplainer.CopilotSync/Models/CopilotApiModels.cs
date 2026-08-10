using System.Text.Json.Serialization;

namespace XafLogicExplainer.CopilotSync.Models;

/// <summary>
/// Request payload for uploading plain text documentation into a Copilot resource.
/// </summary>
public class UploadLongTextRequest
{
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("docName")]
    public string DocName { get; set; } = string.Empty;

    [JsonPropertyName("originalDocName")]
    public string OriginalDocName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;

    [JsonPropertyName("textContent")]
    public string TextContent { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for starting a chat session.
/// </summary>
public class StartChatRequest
{
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("sessionName")]
    public string SessionName { get; set; } = string.Empty;
}

/// <summary>
/// Request payload for sending a user message to an existing chat session.
/// </summary>
public class ChatMessageRequest
{
    [JsonPropertyName("chatSessionId")]
    public int ChatSessionId { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("resourceName")]
    public string ResourceName { get; set; } = string.Empty;

    [JsonPropertyName("userMessage")]
    public string UserMessage { get; set; } = string.Empty;
}

/// <summary>
/// Base API response contract.
/// </summary>
public class ApiResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response returned after creating a chat session.
/// </summary>
public class StartChatResponse : ApiResponse
{
    [JsonPropertyName("chatSessionId")]
    public int ChatSessionId { get; set; }
}

/// <summary>
/// Response returned after posting one chat message.
/// </summary>
public class ChatMessageResponse : ApiResponse
{
    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("chatMessageId")]
    public int ChatMessageId { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

/// <summary>
/// Response returned when listing existing documents in a resource.
/// </summary>
public class DocumentListResponse : ApiResponse
{
    [JsonPropertyName("documents")]
    public List<DocumentInfo>? Documents { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>
/// Metadata for one indexed document in Copilot.
/// </summary>
public class DocumentInfo
{
    [JsonPropertyName("documentId")]
    public int DocumentId { get; set; }

    [JsonPropertyName("documentName")]
    public string DocumentName { get; set; } = string.Empty;

    [JsonPropertyName("originalDocumentName")]
    public string OriginalDocumentName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("chunkCount")]
    public int ChunkCount { get; set; }
}

/// <summary>
/// AI provider details returned by Copilot for chat completion setup.
/// </summary>
public class AiProviderInfo
{
    [JsonPropertyName("companyName")]
    public string? CompanyName { get; set; }

    [JsonPropertyName("providerName")]
    public string? ProviderName { get; set; }

    [JsonPropertyName("aiProviderBaseUrl")]
    public string? AiProviderBaseUrl { get; set; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    [JsonPropertyName("embeddingsModel")]
    public string? EmbeddingsModel { get; set; }
}
