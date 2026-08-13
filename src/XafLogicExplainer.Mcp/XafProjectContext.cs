using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Mcp;

/// <summary>
/// Holds the extracted applications the MCP tools answer questions about.
/// </summary>
/// <remarks>
/// Extraction parses every C# and <c>.xafml</c> file in a project, which is fast but not free, and
/// an agent asks many small questions in a row. So each project is extracted once, on the first
/// question that needs it, and kept.
/// <para>
/// Cached data goes stale while a developer edits, which would make the server confidently wrong —
/// the exact failure this project exists to prevent. Rather than re-reading the tree on every call,
/// the cheap source fingerprint is checked and the cache is dropped when it moves.
/// </para>
/// </remarks>
public sealed class XafProjectContext
{
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IReadOnlyList<XafProjectSource> _sources;

    /// <summary>Creates the context over the configured projects.</summary>
    /// <param name="sources">Projects this server can answer about. Must not be empty.</param>
    public XafProjectContext(IReadOnlyList<XafProjectSource> sources)
    {
        _sources = sources;
    }

    /// <summary>The configured projects, in the order they were supplied.</summary>
    public IReadOnlyList<XafProjectSource> Sources => _sources;

    /// <summary>
    /// Returns an extracted project, parsing it if this is the first request or the source changed.
    /// </summary>
    /// <param name="projectName">
    /// Which project, when several are configured. Null selects the only one, or the first.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">No project matches the name.</exception>
    public async Task<ExtractedProject> GetAsync(string? projectName, CancellationToken cancellationToken = default)
    {
        var source = Resolve(projectName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var fingerprint = ComputeFingerprint(source.Path);

            if (_cache.TryGetValue(source.Name, out var cached) && cached.Fingerprint == fingerprint)
                return cached.Project;

            var extractor = new LogicExtractor();
            var project = extractor.ExtractFromSourceDirectory(source.Path, BuildOptions(source));

            _cache[source.Name] = new CacheEntry(project, fingerprint);
            return project;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Drops cached extractions so the next question re-reads the source.
    /// </summary>
    /// <param name="projectName">A single project, or null for all of them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many cached projects were discarded.</returns>
    public async Task<int> InvalidateAsync(string? projectName, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (projectName is null)
            {
                var count = _cache.Count;
                _cache.Clear();
                return count;
            }

            var source = Resolve(projectName);
            return _cache.Remove(source.Name) ? 1 : 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Resolves a project name to a configured source.
    /// </summary>
    public XafProjectSource Resolve(string? projectName)
    {
        if (_sources.Count == 0)
            throw new InvalidOperationException(
                "No XAF project is configured. Start the server with --project, or run `xaflogic projects add`.");

        if (string.IsNullOrWhiteSpace(projectName))
            return _sources[0];

        var match = _sources.FirstOrDefault(s =>
            s.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            return match;

        var known = string.Join(", ", _sources.Select(s => s.Name));
        throw new InvalidOperationException($"No project named '{projectName}'. Configured: {known}.");
    }

    /// <summary>
    /// Produces a cheap signal that the source tree has changed.
    /// </summary>
    /// <remarks>
    /// Deliberately not the SHA-256 the CLI uses for change detection: that reads every file, which
    /// would cost as much as the extraction it is meant to avoid. Sizes and write times over the
    /// same file set are enough to notice an edit between two questions, and being wrong in the
    /// conservative direction only costs one unnecessary re-parse.
    /// </remarks>
    private static long ComputeFingerprint(string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return 0;

        long fingerprint = 17;

        try
        {
            var files = Directory
                .EnumerateFiles(projectPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".xafml", StringComparison.OrdinalIgnoreCase))
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                fingerprint = unchecked(fingerprint * 31 + info.Length);
                fingerprint = unchecked(fingerprint * 31 + info.LastWriteTimeUtc.Ticks);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be stat'ed should not take the server down mid-conversation.
            // Returning a changing value forces a re-parse, which is the safe direction.
            return DateTime.UtcNow.Ticks;
        }

        return fingerprint;
    }

    private static ExtractionOptions BuildOptions(XafProjectSource source)
    {
        var options = new ExtractionOptions
        {
            IncludeSourceCode = true,
            IncludeMethodBodies = true,
            IncludeComments = true,
            LanguageCode = source.Language,
        };

        options.Orm = source.Orm?.ToLowerInvariant() switch
        {
            "xpo" => OrmType.Xpo,
            "efcore" or "ef" => OrmType.EfCore,
            _ => OrmType.Auto,
        };

        return options;
    }

    private sealed record CacheEntry(ExtractedProject Project, long Fingerprint);
}

/// <summary>
/// One XAF project this server can answer questions about.
/// </summary>
public sealed class XafProjectSource
{
    /// <summary>Name an agent uses to select this project. Defaults to the directory name.</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path to the XAF module directory.</summary>
    public required string Path { get; init; }

    /// <summary>ORM override: <c>xpo</c>, <c>efcore</c>, or null to auto-detect.</summary>
    public string? Orm { get; init; }

    /// <summary>Language for extracted descriptions.</summary>
    public string Language { get; init; } = "en";
}
