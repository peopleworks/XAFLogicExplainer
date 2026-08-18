using XafLogicExplainer.CopilotSync.Ai;
using XafLogicExplainer.CopilotSync.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Which of the routes to a model is taken, and what someone with none of them is told.
/// </summary>
/// <remarks>
/// Every AI feature reached PeopleWorks Copilot for the model's key — not a key the user had
/// configured, but one fetched from an account. On a public MIT project that meant no outside user
/// could run any of them, and the message they met named only the service they had no account with.
/// <para>
/// The environment is read through an injected lookup rather than the process, so the order of the
/// routes can be pinned without setting a variable the rest of the test run would share.
/// </para>
/// </remarks>
public class AiClientResolverTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] set)
    {
        var values = set.ToDictionary(entry => entry.Name, entry => entry.Value, StringComparer.Ordinal);

        return name => values.GetValueOrDefault(name);
    }

    private static readonly Func<string, string?> NoEnvironment = _ => null;

    private static SyncConfiguration Copilot => new()
    {
        CopilotApiBaseUrl = "https://copilot.example.invalid",
        CopilotApiToken = "a-token",
        ResourceName = "SomeResource",
        UserName = "xaf-logic-explainer",
    };

    [Fact]
    public async Task AKeyOnTheCommandLineIsEnough()
    {
        // The whole point: no account, no configuration file, nothing in the environment.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest { ApiKey = "sk-given-here" },
            TestContext.Current.CancellationToken, NoEnvironment);

        Assert.True(resolved.Succeeded);
        Assert.Equal(AiCredentialSource.Explicit, resolved.Source);
    }

    [Fact]
    public async Task AnOpenAiKeyInTheEnvironmentIsEnough()
    {
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest(), TestContext.Current.CancellationToken,
            Env(("OPENAI_API_KEY", "sk-from-the-environment")));

        Assert.True(resolved.Succeeded);
        Assert.Equal(AiCredentialSource.Environment, resolved.Source);
        Assert.Equal("OPENAI_API_KEY", resolved.ProviderName);
        Assert.Equal("gpt-4o-mini", resolved.Model);
    }

    [Fact]
    public async Task AnAnthropicKeyInTheEnvironmentIsEnough()
    {
        // Reached through the same client: Anthropic serves an OpenAI-compatible surface, so
        // supporting it costs a base URL and a default model rather than a second SDK.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest(), TestContext.Current.CancellationToken,
            Env(("ANTHROPIC_API_KEY", "sk-ant-from-the-environment")));

        Assert.True(resolved.Succeeded);
        Assert.Equal(AiCredentialSource.Environment, resolved.Source);
        Assert.Equal("ANTHROPIC_API_KEY", resolved.ProviderName);
        Assert.Equal("claude-sonnet-5", resolved.Model);
    }

    [Fact]
    public async Task TheCommandLineOutranksTheEnvironment()
    {
        // A single run has to be steerable on a machine that already has a key set globally,
        // otherwise the flag is decoration.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest { ApiKey = "sk-given-here" },
            TestContext.Current.CancellationToken,
            Env(("OPENAI_API_KEY", "sk-from-the-environment"),
                ("ANTHROPIC_API_KEY", "sk-ant-from-the-environment")));

        Assert.Equal(AiCredentialSource.Explicit, resolved.Source);
    }

    [Fact]
    public async Task TheEnvironmentOutranksAConfiguredAccount()
    {
        // The account is the last route, not the first. Reaching for it while a key sits in the
        // environment is what made the network call unavoidable, and with it the account.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest { Copilot = Copilot },
            TestContext.Current.CancellationToken,
            Env(("OPENAI_API_KEY", "sk-from-the-environment")));

        Assert.Equal(AiCredentialSource.Environment, resolved.Source);
    }

    [Fact]
    public async Task AnEndpointAndAModelOverrideTheDefaults()
    {
        // What makes a local model, or any OpenAI-compatible gateway, reachable at all.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest
            {
                ApiKey = "not-checked-locally",
                BaseUrl = "http://localhost:11434/v1",
                Model = "qwen2.5-coder",
            },
            TestContext.Current.CancellationToken, NoEnvironment);

        Assert.True(resolved.Succeeded);
        Assert.Equal("qwen2.5-coder", resolved.Model);
    }

    [Fact]
    public async Task AnEmptyAccountIsNotAConfiguredAccount()
    {
        // `xaflogic config` leaves these empty until someone fills them in, and an empty URL with an
        // empty token used to be enough to send the run down a route that could only fail.
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest { Copilot = new SyncConfiguration() },
            TestContext.Current.CancellationToken, NoEnvironment);

        Assert.False(resolved.Succeeded);
        Assert.Equal(AiClientResolver.NothingConfigured, resolved.Problem);
    }

    [Fact]
    public async Task SomeoneWithNoRouteConfiguredIsToldAllOfThem()
    {
        var resolved = await AiClientResolver.ResolveAsync(
            new AiClientRequest(), TestContext.Current.CancellationToken, NoEnvironment);

        Assert.False(resolved.Succeeded);

        var problem = resolved.Problem!;

        // Naming only the one nobody outside this shop has an account with is the defect itself.
        Assert.Contains("--api-key", problem, StringComparison.Ordinal);
        Assert.Contains("OPENAI_API_KEY", problem, StringComparison.Ordinal);
        Assert.Contains("ANTHROPIC_API_KEY", problem, StringComparison.Ordinal);
        Assert.Contains("xaflogic config", problem, StringComparison.Ordinal);
    }
}
