using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Interfaces;

/// <summary>
/// A destination for extracted documentation.
/// </summary>
/// <remarks>
/// Extraction produces one model; where that model ends up is a separate decision. A sink might
/// write files next to the analyzed project, upload to a documentation service, or feed an index.
/// <para>
/// Sinks receive the <see cref="ExtractedProject"/> as well as the rendered sections, because the
/// useful shape differs: a file writer renders its own layout from the model, while an uploader
/// wants sections it can address individually.
/// </para>
/// </remarks>
public interface IDocumentationSink
{
    /// <summary>
    /// Short identifier used in logs and command output, e.g. <c>agent-files</c>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this sink reaches the network. Surfaced so callers can be explicit about which
    /// commands send source-derived documentation off the machine.
    /// </summary>
    bool IsRemote { get; }

    /// <summary>
    /// Publishes documentation for one extracted project.
    /// </summary>
    /// <param name="project">The extracted model.</param>
    /// <param name="sections">Rendered documentation sections.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SinkResult> PublishAsync(
        ExtractedProject project,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a single <see cref="IDocumentationSink.PublishAsync"/> call.
/// </summary>
public sealed class SinkResult
{
    /// <summary>Whether publishing completed.</summary>
    public bool Success { get; init; }

    /// <summary>One line suitable for console output.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// What was produced: file paths for a local sink, document identifiers for a remote one.
    /// </summary>
    public IReadOnlyList<string> Artifacts { get; init; } = [];

    /// <summary>Populated when <see cref="Success"/> is false.</summary>
    public string? Error { get; init; }

    /// <summary>Creates a successful result.</summary>
    public static SinkResult Ok(string summary, IReadOnlyList<string>? artifacts = null) =>
        new() { Success = true, Summary = summary, Artifacts = artifacts ?? [] };

    /// <summary>Creates a failed result.</summary>
    public static SinkResult Fail(string error) =>
        new() { Success = false, Summary = error, Error = error };
}
