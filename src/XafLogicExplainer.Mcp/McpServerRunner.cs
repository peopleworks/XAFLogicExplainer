using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Mcp;

/// <summary>
/// Hosts the XAF MCP server over stdio.
/// </summary>
public static class McpServerRunner
{
    /// <summary>
    /// Runs the server until the client disconnects.
    /// </summary>
    /// <param name="sources">The XAF projects this server can answer questions about.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RunStdioAsync(
        IReadOnlyList<XafProjectSource> sources,
        CancellationToken cancellationToken = default)
    {
        var builder = Host.CreateApplicationBuilder();

        // stdout is the JSON-RPC channel. A single stray log line written there corrupts the
        // protocol stream and the client drops the connection with a parse error that points
        // nowhere near the cause -- so every log goes to stderr, including Information.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        builder.Services.AddSingleton(new XafProjectContext(sources));

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<XafDiscoveryTools>()
            .WithTools<XafDetailTools>();

        await builder.Build().RunAsync(cancellationToken);
    }
}
