using System.Net.Http.Json;
using System.Text.Json;
using XafLogicExplainer.CopilotSync.Models;

namespace XafLogicExplainer.CopilotSync.Services;

/// <summary>
/// Thin HTTP client wrapper for PeopleWorks Copilot API endpoints used by this solution.
/// </summary>
public class CopilotApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _userName;
    private readonly string _resourceName;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Creates a configured Copilot API client instance.
    /// </summary>
    /// <param name="baseUrl">API base URL.</param>
    /// <param name="token">Authentication token value for the <c>Token</c> header.</param>
    /// <param name="userName">Default user name for requests.</param>
    /// <param name="resourceName">Target resource name for requests.</param>
    public CopilotApiClient(string baseUrl, string token, string userName, string resourceName)
    {
        _userName = userName;
        _resourceName = resourceName;
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(300)
        };
        _http.DefaultRequestHeaders.Add("Token", token);
    }

    /// <summary>
    /// Upload long text content as a document to the Copilot knowledge base.
    /// POST /ResourceFile/uploadlongtext
    /// </summary>
    public async Task<ApiResponse> UploadLongTextAsync(string docName, string originalDocName,
        string description, string tags, string textContent)
    {
        var request = new UploadLongTextRequest
        {
            UserName = _userName,
            ResourceName = _resourceName,
            DocName = docName,
            OriginalDocName = originalDocName,
            Description = description,
            Tags = tags,
            TextContent = textContent
        };

        var response = await _http.PostAsJsonAsync("ResourceFile/uploadlongtext", request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"HTTP {(int)response.StatusCode}: {content}"
            };
        }

        return JsonSerializer.Deserialize<ApiResponse>(content, JsonOptions)
               ?? new ApiResponse { Success = false, Message = "Failed to parse response" };
    }

    /// <summary>
    /// List all documents in the resource.
    /// GET /ResourceFile/list?userName=...&amp;resourceName=...
    /// </summary>
    public async Task<DocumentListResponse> ListDocumentsAsync()
    {
        var url = $"ResourceFile/list?userName={Uri.EscapeDataString(_userName)}&resourceName={Uri.EscapeDataString(_resourceName)}";
        var response = await _http.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new DocumentListResponse
            {
                Success = false,
                Message = $"HTTP {(int)response.StatusCode}: {content}"
            };
        }

        return JsonSerializer.Deserialize<DocumentListResponse>(content, JsonOptions)
               ?? new DocumentListResponse { Success = false, Message = "Failed to parse response" };
    }

    /// <summary>
    /// Start a chat session.
    /// POST /Chat/start
    /// </summary>
    public async Task<StartChatResponse> StartChatAsync(string sessionName)
    {
        var request = new StartChatRequest
        {
            UserName = _userName,
            ResourceName = _resourceName,
            SessionName = sessionName
        };

        var response = await _http.PostAsJsonAsync("Chat/start", request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new StartChatResponse
            {
                Success = false,
                Message = $"HTTP {(int)response.StatusCode}: {content}"
            };
        }

        return JsonSerializer.Deserialize<StartChatResponse>(content, JsonOptions)
               ?? new StartChatResponse { Success = false, Message = "Failed to parse response" };
    }

    /// <summary>
    /// Send a chat message and get a response.
    /// POST /Chat/message
    /// </summary>
    public async Task<ChatMessageResponse> SendChatMessageAsync(int chatSessionId, string userMessage)
    {
        var request = new ChatMessageRequest
        {
            ChatSessionId = chatSessionId,
            UserName = _userName,
            ResourceName = _resourceName,
            UserMessage = userMessage
        };

        var response = await _http.PostAsJsonAsync("Chat/message", request);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new ChatMessageResponse
            {
                Success = false,
                Message = $"HTTP {(int)response.StatusCode}: {content}"
            };
        }

        return JsonSerializer.Deserialize<ChatMessageResponse>(content, JsonOptions)
               ?? new ChatMessageResponse { Success = false, Message = "Failed to parse response" };
    }

    /// <summary>
    /// Get AI provider configuration for the company.
    /// GET /GetAIProviderByCompany
    /// </summary>
    public async Task<AiProviderInfo?> GetAiProviderAsync()
    {
        var response = await _http.GetAsync("GetAIProviderByCompany");
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return null;

        var providers = JsonSerializer.Deserialize<List<AiProviderInfo>>(content, JsonOptions);
        return providers?.FirstOrDefault();
    }

    /// <summary>
    /// Disposes underlying HTTP resources.
    /// </summary>
    public void Dispose()
    {
        _http.Dispose();
    }
}
