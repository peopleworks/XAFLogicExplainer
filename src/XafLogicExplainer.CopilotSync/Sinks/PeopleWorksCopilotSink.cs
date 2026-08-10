using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.CopilotSync.Services;

namespace XafLogicExplainer.CopilotSync.Sinks;

/// <summary>
/// Publishes extracted documentation to a PeopleWorks Copilot resource.
/// </summary>
/// <remarks>
/// A thin adapter over <see cref="DocumentationUploader"/>, which holds the upload logic and is
/// unchanged by this type. Its purpose is to make the remote target one
/// <see cref="IDocumentationSink"/> among several rather than the destination the tool is built
/// around, so a caller can choose where documentation goes without knowing what it is talking to.
/// </remarks>
public sealed class PeopleWorksCopilotSink : IDocumentationSink
{
    private readonly DocumentationUploader _uploader;
    private readonly Action<string>? _onProgress;

    /// <summary>Creates the sink.</summary>
    /// <param name="uploader">The configured uploader.</param>
    /// <param name="onProgress">Optional progress callback, surfaced to the console by the CLI.</param>
    public PeopleWorksCopilotSink(DocumentationUploader uploader, Action<string>? onProgress = null)
    {
        _uploader = uploader;
        _onProgress = onProgress;
    }

    /// <inheritdoc />
    public string Name => "peopleworks-copilot";

    /// <inheritdoc />
    /// <remarks>
    /// True, and deliberately visible: this sink sends documentation derived from the analyzed
    /// source code to a remote service, which is a decision a caller should be able to see before
    /// making it.
    /// </remarks>
    public bool IsRemote => true;

    /// <inheritdoc />
    public async Task<SinkResult> PublishAsync(
        ExtractedProject project,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        // The uploader renders its own sections from the project, because it also produces a
        // combined document and needs the two to agree. The sections passed in are therefore
        // unused here -- other sinks want them, this one does not.
        var result = await _uploader.UploadProjectDocumentationAsync(project, _onProgress);

        if (!result.Success)
        {
            var detail = result.Errors.Count > 0
                ? string.Join("; ", result.Errors)
                : result.Message;

            return SinkResult.Fail($"Upload failed: {detail}");
        }

        return SinkResult.Ok(
            $"{result.DocumentsUploaded} documents uploaded",
            result.UploadedDocuments.Select(d => d.FileName).ToList());
    }
}
