namespace XafLogicExplainer.Blazor;

/// <summary>
/// Configuration options for the PeopleWorks Copilot Widget panel.
/// </summary>
public class CopilotWidgetOptions
{
    /// <summary>
    /// Base URL of the PeopleWorks Copilot Widget (deployed instance).
    /// Example: "https://peopleworkscopilotwidget.peopleworksservices.com"
    /// </summary>
    public string WidgetBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Base URL of the PeopleWorks Copilot API.
    /// Example: "https://copilotapi.peopleworksservices.com/"
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Company API token for authentication with PeopleWorks Copilot.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Resource name in PeopleWorks Copilot (e.g., "MyApplication").
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// Default user name for the chat session.
    /// If empty, will attempt to use the current XAF user's name.
    /// </summary>
    public string DefaultUserName { get; set; } = "Usuario";

    /// <summary>
    /// Width of the Copilot panel in pixels.
    /// </summary>
    public int PanelWidth { get; set; } = 420;

    /// <summary>
    /// Label shown on the floating tab button.
    /// </summary>
    public string TabLabel { get; set; } = "Copilot";

    /// <summary>
    /// Build the full iframe URL with parameters for the /assist route.
    /// </summary>
    /// <param name="userName">Optional runtime user name override.</param>
    /// <returns>Fully composed widget URL including encoded query parameters.</returns>
    public string BuildAssistUrl(string? userName = null)
    {
        var user = Uri.EscapeDataString(userName ?? DefaultUserName);
        var resource = Uri.EscapeDataString(ResourceName);
        var baseUrl = Uri.EscapeDataString(ApiBaseUrl.TrimEnd('/') + "/");

        return $"{WidgetBaseUrl.TrimEnd('/')}/assist?apiKey={ApiKey}&resourceName={resource}&userName={user}&baseUrl={baseUrl}";
    }
}
