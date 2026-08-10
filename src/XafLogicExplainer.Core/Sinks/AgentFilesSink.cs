using System.Text;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Sinks;

/// <summary>
/// Writes agent-readable context files next to the analyzed application.
/// </summary>
/// <remarks>
/// The sink that needs no infrastructure: no account, no server, no API key. It produces the files
/// coding agents already look for, so the extraction becomes useful the moment it finishes.
/// <para>
/// Output is tiered. <c>AGENTS.md</c> holds a compact index that agents load on every request;
/// the bulky detail goes to <c>.xaflogic/</c>, which they open only when a question needs it.
/// </para>
/// </remarks>
public sealed class AgentFilesSink : IDocumentationSink
{
    private readonly AgentFilesOptions _options;
    private readonly AgentContextGenerator _generator;

    /// <summary>Creates the sink.</summary>
    /// <param name="options">Where and what to write.</param>
    /// <param name="toolVersion">Version stamped into generated headers.</param>
    public AgentFilesSink(AgentFilesOptions options, string toolVersion = "0.9.0")
    {
        _options = options;
        _generator = new AgentContextGenerator(toolVersion);
    }

    /// <inheritdoc />
    public string Name => "agent-files";

    /// <inheritdoc />
    /// <remarks>Writes to disk only. This sink never reaches the network.</remarks>
    public bool IsRemote => false;

    /// <inheritdoc />
    public Task<SinkResult> PublishAsync(
        ExtractedProject project,
        IReadOnlyList<DocumentSection> sections,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var root = ResolveRoot(project);
            var written = new List<string>();

            // Detail tier first: the index links to these by name, so they must be settled before
            // it is generated.
            var detailNames = WriteDetailFiles(root, sections, written, cancellationToken);

            var index = _generator.GenerateIndex(project, detailNames);

            if (_options.WriteAgentsMd)
                WriteManaged(Path.Combine(root, "AGENTS.md"), index, written);

            if (_options.WriteClaudeMd)
                WriteManaged(Path.Combine(root, "CLAUDE.md"), BuildClaudePointer(), written);

            if (_options.WriteCopilotInstructions)
            {
                var copilotPath = Path.Combine(root, ".github", "copilot-instructions.md");
                Directory.CreateDirectory(Path.GetDirectoryName(copilotPath)!);
                WriteManaged(copilotPath, BuildCopilotInstructions(project), written);
            }

            var summary = $"{written.Count} {(written.Count == 1 ? "file" : "files")} written to {root}";
            return Task.FromResult(SinkResult.Ok(summary, written));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Documentation generation must never be the reason a developer's workflow stops.
            return Task.FromResult(SinkResult.Fail($"Could not write agent files: {ex.Message}"));
        }
    }

    /// <summary>
    /// Decides where <c>AGENTS.md</c> belongs.
    /// </summary>
    /// <remarks>
    /// An XAF module is a subdirectory of the solution, but agents look for context at the root of
    /// the repository. Writing it inside <c>MyApp.Module/</c> puts it where nothing will read it,
    /// so walk up to the solution or repository root and place it there instead.
    /// </remarks>
    private string ResolveRoot(ExtractedProject project)
    {
        if (!string.IsNullOrWhiteSpace(_options.OutputRoot))
            return _options.OutputRoot!;

        var current = new DirectoryInfo(project.ProjectPath);

        // Bounded walk: deep enough to clear a module inside a solution, shallow enough that a
        // misconfigured path cannot wander up to the drive root and write there.
        for (var depth = 0; depth < 5 && current is not null; depth++)
        {
            var isRepositoryRoot =
                Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                current.EnumerateFiles("*.sln").Any() ||
                current.EnumerateFiles("*.slnx").Any();

            if (isRepositoryRoot)
                return current.FullName;

            current = current.Parent;
        }

        // No solution or repository found. The project directory is a defensible fallback and,
        // more importantly, is certain to exist.
        return project.ProjectPath;
    }

    private List<string> WriteDetailFiles(
        string root,
        IReadOnlyList<DocumentSection> sections,
        List<string> written,
        CancellationToken cancellationToken)
    {
        var detailRoot = Path.Combine(root, AgentContextGenerator.DetailFolder);
        Directory.CreateDirectory(detailRoot);

        var names = new List<string>();

        foreach (var section in sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = $"{Sanitize(section.FileName)}.md";
            var path = Path.Combine(detailRoot, fileName);

            var content = new StringBuilder();
            content.AppendLine($"# {section.Title}");
            content.AppendLine();

            if (!string.IsNullOrWhiteSpace(section.Description))
            {
                content.AppendLine($"> {section.Description}");
                content.AppendLine();
            }

            content.AppendLine(section.Content.TrimEnd());
            content.AppendLine();
            content.AppendLine("---");
            content.AppendLine();
            content.AppendLine("Generated by XAF Logic Explainer. Edits are overwritten; see `AGENTS.md`.");

            // Wholly generated files, unlike AGENTS.md -- nobody hand-edits these, so a plain
            // overwrite is correct and keeps them clean.
            File.WriteAllText(path, content.ToString());

            names.Add(fileName);
            written.Add(path);
        }

        return names;
    }

    private static void WriteManaged(string path, string generatedContent, List<string> written)
    {
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;
        File.WriteAllText(path, ManagedBlock.Apply(existing, generatedContent));
        written.Add(path);
    }

    /// <summary>
    /// Builds <c>CLAUDE.md</c>, which imports rather than duplicates.
    /// </summary>
    /// <remarks>
    /// Claude Code resolves <c>@path</c> imports, so the content can live in exactly one place.
    /// Two copies of the same generated context would drift the moment someone regenerated only one.
    /// </remarks>
    private static string BuildClaudePointer()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## This XAF application");
        sb.AppendLine();
        sb.AppendLine("@AGENTS.md");
        sb.AppendLine();
        sb.AppendLine("That file is generated from the application's own source: its entities, controllers,");
        sb.AppendLine("actions, business rules and Model Editor customizations. Treat its inventories as");
        sb.AppendLine("complete, and regenerate it with `xaflogic agents` when the code has moved on.");
        return sb.ToString();
    }

    /// <summary>
    /// Builds the GitHub Copilot instructions file.
    /// </summary>
    /// <remarks>
    /// Copilot has no import mechanism, so a pointer alone would leave it with nothing. The ground
    /// rules are small and carry most of the value, so they are inlined here and the rest is left
    /// as a file reference Copilot can open when it needs to.
    /// </remarks>
    private string BuildCopilotInstructions(ExtractedProject project)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {project.ProjectName}");
        sb.AppendLine();
        sb.AppendLine("This repository is a DevExpress XAF application. The rules below are extracted from its");
        sb.AppendLine("own source code and take precedence over general XAF guidance.");
        sb.AppendLine();
        sb.Append(_generator.GenerateGroundRules(project));
        sb.AppendLine("The complete inventory of entities, controllers and actions — plus conventions, criteria");
        sb.AppendLine("examples and step-by-step recipes for common changes — is in `AGENTS.md` at the repository");
        sb.AppendLine("root. Read it before answering questions about this application's structure.");
        return sb.ToString();
    }

    /// <summary>
    /// Makes a section name safe as a file name on every platform.
    /// </summary>
    private static string Sanitize(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "section" : cleaned;
    }
}

/// <summary>
/// Controls what <see cref="AgentFilesSink"/> writes and where.
/// </summary>
public sealed class AgentFilesOptions
{
    /// <summary>
    /// Where to write. When null, the solution or repository root above the project is used.
    /// </summary>
    public string? OutputRoot { get; init; }

    /// <summary>Write <c>AGENTS.md</c>, the cross-agent standard. Defaults to true.</summary>
    public bool WriteAgentsMd { get; init; } = true;

    /// <summary>Write <c>CLAUDE.md</c> as an import of <c>AGENTS.md</c>. Defaults to true.</summary>
    public bool WriteClaudeMd { get; init; } = true;

    /// <summary>Write <c>.github/copilot-instructions.md</c>. Defaults to true.</summary>
    public bool WriteCopilotInstructions { get; init; } = true;
}
