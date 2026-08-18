using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using XafLogicExplainer.CopilotSync.Models;
using XafLogicExplainer.CopilotSync.Services;

namespace XafLogicExplainer.CopilotSync.Ai;

/// <summary>Where the credentials behind a chat client came from.</summary>
public enum AiCredentialSource
{
    /// <summary>Passed on the command line.</summary>
    Explicit,

    /// <summary>Read from the environment.</summary>
    Environment,

    /// <summary>Fetched from a PeopleWorks Copilot account.</summary>
    PeopleWorksCopilot,
}

/// <summary>What the caller knows about which model to talk to.</summary>
public sealed record AiClientRequest
{
    /// <summary>A key supplied directly, which outranks everything else.</summary>
    public string? ApiKey { get; init; }

    /// <summary>Any OpenAI-compatible endpoint, including a local one.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>The model name, when the caller wants one other than the default.</summary>
    public string? Model { get; init; }

    /// <summary>A PeopleWorks Copilot account, used only when nothing else is configured.</summary>
    public SyncConfiguration? Copilot { get; init; }
}

/// <summary>A chat client and the account it belongs to, or the reason there is none.</summary>
public sealed record AiClientResolution
{
    /// <summary>The client, when one could be built.</summary>
    public IChatClient? Client { get; init; }

    /// <summary>The provider's name, for the line printed before work starts.</summary>
    public string ProviderName { get; init; } = "";

    /// <summary>The model that will answer.</summary>
    public string Model { get; init; } = "";

    /// <summary>Which of the configured routes was taken.</summary>
    public AiCredentialSource Source { get; init; }

    /// <summary>Why there is no client, written for the person who has to fix it.</summary>
    public string? Problem { get; init; }

    /// <summary>Whether a client was obtained.</summary>
    public bool Succeeded => Client is not null;
}

/// <summary>
/// Finds a model to talk to.
/// </summary>
/// <remarks>
/// Every AI feature here used to reach PeopleWorks Copilot for its credentials — not for a key the
/// user had configured, but for the model's key, fetched from an account. On a public MIT project
/// that meant no outside user could run any of them: <c>--enrich</c> refused without an API URL and
/// token, and told the reader to configure credentials for a service they had never heard of.
/// <para>
/// The order is explicit first, then the environment, then the account — so a key on the command
/// line always wins, a machine with <c>OPENAI_API_KEY</c> already set works with no configuration
/// at all, and an existing PeopleWorks Copilot setup keeps working untouched. It is one option
/// among several now rather than the gate.
/// </para>
/// <para>
/// A key is never read from, or written to, the configuration file. The endpoint and the model name
/// are ordinary settings; a key is a secret, and that file lives in a home directory that gets
/// copied around. The environment is where a secret belongs.
/// </para>
/// </remarks>
public static class AiClientResolver
{
    private const string OpenAiEndpoint = "https://api.openai.com/v1";
    private const string OpenAiModel = "gpt-4o-mini";

    // Anthropic serves an OpenAI-compatible surface at this path, so the same client reaches it and
    // a second SDK is not needed.
    private const string AnthropicEndpoint = "https://api.anthropic.com/v1";
    private const string AnthropicModel = "claude-sonnet-5";

    /// <summary>What to tell someone who has configured none of the routes.</summary>
    public const string NothingConfigured =
        "No AI provider is configured. Any one of these is enough:\n"
        + "\n"
        + "  --api-key <key>      a key passed directly, with --ai-base-url for any\n"
        + "                       OpenAI-compatible endpoint, including a local one\n"
        + "  OPENAI_API_KEY       set in the environment\n"
        + "  ANTHROPIC_API_KEY    set in the environment\n"
        + "  xaflogic config      an existing PeopleWorks Copilot account";

    /// <summary>Builds a chat client from the first route that is configured.</summary>
    /// <param name="request">What the caller was told on the command line.</param>
    /// <param name="cancellationToken">Cancels the account lookup, which is a network call.</param>
    /// <param name="environment">
    /// How to read an environment variable. Injected so the order of the routes can be tested
    /// without setting process-wide state, which the whole test run would then share.
    /// </param>
    public static async Task<AiClientResolution> ResolveAsync(
        AiClientRequest request,
        CancellationToken cancellationToken = default,
        Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        if (request.ApiKey is { Length: > 0 } explicitKey)
        {
            return Build(explicitKey, request.BaseUrl ?? OpenAiEndpoint, request.Model ?? OpenAiModel,
                         "the key passed on the command line", AiCredentialSource.Explicit);
        }

        if (environment("OPENAI_API_KEY") is { Length: > 0 } openAiKey)
        {
            return Build(openAiKey, request.BaseUrl ?? OpenAiEndpoint, request.Model ?? OpenAiModel,
                         "OPENAI_API_KEY", AiCredentialSource.Environment);
        }

        if (environment("ANTHROPIC_API_KEY") is { Length: > 0 } anthropicKey)
        {
            return Build(anthropicKey, request.BaseUrl ?? AnthropicEndpoint, request.Model ?? AnthropicModel,
                         "ANTHROPIC_API_KEY", AiCredentialSource.Environment);
        }

        if (request.Copilot is { CopilotApiBaseUrl.Length: > 0, CopilotApiToken.Length: > 0 } copilot)
            return await FromCopilotAsync(copilot, request, cancellationToken);

        return new AiClientResolution { Problem = NothingConfigured };
    }

    /// <summary>The original route: the account holds the provider, and hands back its key.</summary>
    private static async Task<AiClientResolution> FromCopilotAsync(
        SyncConfiguration copilot, AiClientRequest request, CancellationToken cancellationToken)
    {
        AiProviderInfo? provider;

        try
        {
            provider = await new IncrementalSyncService().GetAiProviderAsync(copilot);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AiClientResolution
            {
                Problem = $"PeopleWorks Copilot did not answer: {ex.Message}\n\n{NothingConfigured}",
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (provider is null || string.IsNullOrEmpty(provider.ApiKey))
        {
            return new AiClientResolution
            {
                Problem = "PeopleWorks Copilot returned no AI provider. The token may be invalid, or the "
                          + $"account may have no provider configured.\n\n{NothingConfigured}",
            };
        }

        // The command line still wins over whatever the account is set to, so someone who has an
        // account can still point a single run somewhere else.
        var model = request.Model
                    ?? provider.Parameters?.GetValueOrDefault("model")?.ToString()
                    ?? OpenAiModel;

        return Build(provider.ApiKey,
                     request.BaseUrl ?? provider.AiProviderBaseUrl ?? OpenAiEndpoint,
                     model,
                     provider.ProviderName ?? "PeopleWorks Copilot",
                     AiCredentialSource.PeopleWorksCopilot);
    }

    private static AiClientResolution Build(
        string apiKey, string baseUrl, string model, string providerName, AiCredentialSource source)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        return new AiClientResolution
        {
            Client = client.GetChatClient(model).AsIChatClient(),
            ProviderName = providerName,
            Model = model,
            Source = source,
        };
    }
}
