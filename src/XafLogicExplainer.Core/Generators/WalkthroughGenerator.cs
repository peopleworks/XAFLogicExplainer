using System.Text;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Writes one business process out as a document.
/// </summary>
/// <remarks>
/// Five parts, in this order and for this reason: the diagram, because it is what a reader looks at
/// first; what takes part, so that nothing named in the document is left without a place to go and
/// read it; the steps, because each one carries the place it can be checked; what the walk could not
/// follow, because a gap named is a gap the reader can go and close; and what the walk is not,
/// because a document that describes its own bounds cannot be mistaken for one that has none.
/// <para>
/// No model is involved. Everything here is a rendering of a slice that was computed, which is what
/// makes the whole document diffable between two extractions.
/// </para>
/// </remarks>
public sealed class WalkthroughGenerator
{
    private readonly DocumentationLabels _l;

    /// <summary>Creates the generator for one language.</summary>
    public WalkthroughGenerator(string language = "es") => _l = DocumentationLabels.ForLanguage(language);

    /// <summary>Renders the walkthrough, or the reason there is none.</summary>
    public string Generate(ExtractedProject project, ProcessSlice slice)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {_l.Walkthrough} — {slice.Seed}");
        sb.AppendLine();

        if (!slice.Found)
        {
            sb.AppendLine(slice.Problem ?? _l.WalkthroughNotFound);

            return sb.ToString();
        }

        AppendOpening(sb, project, slice);
        AppendFlow(sb, slice);
        AppendCast(sb, project, slice);
        AppendSteps(sb, project, slice);
        AppendUnresolved(sb, slice);
        AppendBounds(sb, slice);

        return sb.ToString();
    }

    private void AppendOpening(StringBuilder sb, ExtractedProject project, ProcessSlice slice)
    {
        var root = slice.Root!;

        sb.AppendLine($"**{_l.Project}:** {project.ProjectName}");
        sb.AppendLine();
        sb.AppendLine($"{_l.StartedFrom} **{root.Name}** — {KindName(root.Kind)}, {At(project, root)}.");
        sb.AppendLine();

        // How far it went, and -- the half that matters -- why it stopped. Three answers, not two:
        // the process ended, the walk's limit ended it, or the source stopped being decidable. The
        // third had been reading as the first, so the opening sentence claimed a completeness the
        // section below it went on to deny.
        sb.AppendLine(
            slice.DepthReached ? string.Format(_l.ReachedAndStopped, slice.Nodes.Count, slice.Depth)
            : slice.Unresolved.Count > 0 ? string.Format(_l.ReachedAndBlocked, slice.Nodes.Count, slice.Depth)
            : string.Format(_l.ReachedAndFinished, slice.Nodes.Count, slice.Depth));

        sb.AppendLine();
    }

    private void AppendFlow(StringBuilder sb, ProcessSlice slice)
    {
        var diagram = MermaidFlow.From(slice, _l);

        if (diagram.Length == 0)
            return;

        sb.AppendLine($"## {_l.Flow}");
        sb.AppendLine();
        sb.Append(diagram);
        sb.AppendLine();
    }

    /// <summary>
    /// Everything the walk reached, each with the place it is declared.
    /// </summary>
    /// <remarks>
    /// A step cites its target, because the target is what the step introduces. That leaves a node
    /// which is only ever a source — a controller, most often — named in the document with nowhere
    /// to go and read it. This section is where every node gets its place, exactly once.
    /// </remarks>
    private void AppendCast(StringBuilder sb, ExtractedProject project, ProcessSlice slice)
    {
        sb.AppendLine($"## {_l.WhatTakesPart}");
        sb.AppendLine();

        foreach (var node in slice.Nodes)
            sb.AppendLine($"- **{node.Name}** — {KindName(node.Kind)}, {At(project, node)}");

        sb.AppendLine();
    }

    private void AppendSteps(StringBuilder sb, ExtractedProject project, ProcessSlice slice)
    {
        if (slice.Edges.Count == 0)
            return;

        var byId = slice.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        sb.AppendLine($"## {_l.StepByStep}");
        sb.AppendLine();

        var step = 0;

        foreach (var edge in slice.Edges)
        {
            if (!byId.TryGetValue(edge.From, out var from) || !byId.TryGetValue(edge.To, out var to))
                continue;

            // The citation is the target's, because the target is what the step introduces.
            sb.AppendLine($"{++step}. **{from.Name}** {Verb(edge.Kind)} **{to.Name}** — {At(project, to)}");
        }

        sb.AppendLine();
    }

    private void AppendUnresolved(StringBuilder sb, ProcessSlice slice)
    {
        sb.AppendLine($"## {_l.CouldNotFollow}");
        sb.AppendLine();

        if (slice.Unresolved.Count == 0)
        {
            sb.AppendLine(_l.EverythingResolved);
            sb.AppendLine();

            return;
        }

        var byId = slice.Nodes.ToDictionary(node => node.Id, node => node.Name, StringComparer.Ordinal);

        foreach (var call in slice.Unresolved)
        {
            var caller = byId.GetValueOrDefault(call.From, call.From);

            sb.AppendLine($"- **`{call.CallName}`**, {_l.CalledFrom} **{caller}** — {call.Reason}.");
            sb.AppendLine($"  - {_l.CouldBe}: {string.Join(", ", call.Candidates.Select(name => $"`{name}`"))}");
        }

        sb.AppendLine();
    }

    private void AppendBounds(StringBuilder sb, ProcessSlice slice)
    {
        sb.AppendLine($"## {_l.WhatThisIsNot}");
        sb.AppendLine();
        sb.AppendLine(slice.DepthReached
            ? string.Format(_l.BoundHit, slice.Depth)
            : string.Format(_l.BoundNotHit, slice.Depth));
        sb.AppendLine();
        sb.AppendLine($"- {_l.BoundNoRelationships}");
        sb.AppendLine($"- {_l.BoundNoSiblings}");
        sb.AppendLine($"- {_l.BoundControllersOnly}");
        sb.AppendLine();
    }

    /// <summary>
    /// A citation the reader can follow, relative to the project.
    /// </summary>
    /// <remarks>
    /// Relative rather than absolute, unlike the MCP tools: this is a document that gets committed,
    /// read on another machine and pasted into an issue, and an absolute path is noise in all three.
    /// The tools serve an agent standing in the source tree, which is a different reader.
    /// </remarks>
    private static string At(ExtractedProject project, SliceNode node)
    {
        if (node.FilePath.Length == 0)
            return "";

        var path = node.FilePath;

        if (project.ProjectPath.Length > 0
            && path.StartsWith(project.ProjectPath, StringComparison.OrdinalIgnoreCase))
        {
            path = path[project.ProjectPath.Length..].TrimStart('/', '\\');
        }

        // Forward slashes whatever the machine wrote them with. This document gets committed and
        // read elsewhere, and a separator that changes with the extractor's operating system would
        // put a diff in every citation the day someone else regenerates it.
        path = path.Replace('\\', '/');

        return node.Line > 0 ? $"`{path}:{node.Line}`" : $"`{path}`";
    }

    private string Verb(SliceEdgeKind kind) => kind switch
    {
        SliceEdgeKind.Declares => _l.StepDeclares,
        SliceEdgeKind.Calls => _l.StepCalls,
        SliceEdgeKind.Touches => _l.StepTouches,
        SliceEdgeKind.Targets => _l.StepTargets,
        SliceEdgeKind.Governs => _l.StepGoverns,
        _ => "",
    };

    private string KindName(SliceNodeKind kind) => kind switch
    {
        SliceNodeKind.Action => _l.KindAction,
        SliceNodeKind.Method => _l.KindMethod,
        SliceNodeKind.Controller => _l.KindController,
        SliceNodeKind.Entity => _l.KindEntity,
        SliceNodeKind.ValidationRule => _l.KindValidationRule,
        SliceNodeKind.AppearanceRule => _l.KindAppearanceRule,
        _ => "",
    };
}
