using System.Text;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Draws a slice as a Mermaid flowchart.
/// </summary>
/// <remarks>
/// Emitted from the walk's own edge set, node for node and arrow for arrow. Nothing here decides
/// what to draw.
/// <para>
/// That is the point rather than an implementation detail. Ask a language model for a Mermaid
/// diagram of a process and it will produce one — including edges that do not exist, drawn with a
/// confidence indistinguishable from the true ones, in a format whose whole value is that a reader
/// believes it at a glance. A diagram emitted from a real subgraph is correct by construction. A
/// model may one day write the prose around it; it may not add an arrow.
/// </para>
/// </remarks>
public static class MermaidFlow
{
    /// <summary>Renders the slice, or nothing at all when there is no edge to draw.</summary>
    public static string From(ProcessSlice slice, DocumentationLabels labels)
    {
        if (slice.Edges.Count == 0)
            return "";

        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var sb = new StringBuilder();

        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart TD");

        foreach (var node in slice.Nodes)
        {
            ids[node.Id] = $"n{ids.Count}";
            sb.AppendLine($"    {ids[node.Id]}{Shape(node.Kind, Escape(node.Name))}");
        }

        sb.AppendLine();

        foreach (var edge in slice.Edges)
        {
            // An edge can only be drawn between two nodes that are in the slice. The walk never
            // records one that is not, and drawing a dangling arrow would invent a node.
            if (ids.TryGetValue(edge.From, out var from) && ids.TryGetValue(edge.To, out var to))
                sb.AppendLine($"    {from} -->|{Verb(edge.Kind, labels)}| {to}");
        }

        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// The outline a kind of node is drawn with.
    /// </summary>
    /// <remarks>
    /// Carrying meaning rather than decoration: a reader can tell a stored thing from a command
    /// without reading a legend, which is most of what a diagram is for.
    /// </remarks>
    private static string Shape(SliceNodeKind kind, string label) => kind switch
    {
        SliceNodeKind.Action => $"([\"{label}\"])",
        SliceNodeKind.Controller => $"[[\"{label}\"]]",
        SliceNodeKind.Entity => $"[(\"{label}\")]",
        SliceNodeKind.ValidationRule or SliceNodeKind.AppearanceRule => $"{{{{\"{label}\"}}}}",
        _ => $"[\"{label}\"]",
    };

    private static string Verb(SliceEdgeKind kind, DocumentationLabels labels) => kind switch
    {
        SliceEdgeKind.Declares => labels.StepDeclares,
        SliceEdgeKind.Calls => labels.StepCalls,
        SliceEdgeKind.Touches => labels.StepTouches,
        SliceEdgeKind.Targets => labels.StepTargets,
        SliceEdgeKind.Governs => labels.StepGoverns,
        _ => "",
    };

    /// <summary>
    /// Makes a label safe inside a quoted Mermaid node.
    /// </summary>
    /// <remarks>
    /// A rule's name is not an identifier — an unnamed appearance rule reads "appearance on
    /// ChangedBy" — so it can carry the characters that end a node early and produce a diagram that
    /// does not render at all.
    /// </remarks>
    private static string Escape(string label) =>
        label.Replace("\"", "#quot;", StringComparison.Ordinal)
             .Replace("<", "#lt;", StringComparison.Ordinal)
             .Replace(">", "#gt;", StringComparison.Ordinal);
}
