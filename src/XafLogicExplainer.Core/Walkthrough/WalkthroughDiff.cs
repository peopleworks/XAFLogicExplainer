using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Walkthrough;

/// <summary>One thing that is different about a process than it was.</summary>
/// <param name="Node">The node as it stands now, or as it stood before when it is gone.</param>
/// <param name="Kind">Whether it arrived, left, or was edited in place.</param>
public sealed record ProcessChange(SliceNode Node, ProcessChangeKind Kind);

/// <summary>How a node differs between two walks.</summary>
public enum ProcessChangeKind
{
    /// <summary>It takes part now and did not before.</summary>
    Added,

    /// <summary>It took part before and does not now.</summary>
    Removed,

    /// <summary>It takes part in both, and what it does is not what it did.</summary>
    Changed,
}

/// <summary>
/// What is different about one process between two extractions.
/// </summary>
/// <remarks>
/// The thing no conversational agent can answer, because none of them has a yesterday. Asked what
/// changed in the commission calculation since the last release, a model can only re-read today's
/// code and describe it confidently; this re-walks the same seed over a stored snapshot and reports
/// the difference between two computed sets.
/// </remarks>
public sealed record WalkthroughDiff
{
    /// <summary>The walk as it stands now.</summary>
    public required ProcessSlice Current { get; init; }

    /// <summary>The walk as it stood at the snapshot.</summary>
    public required ProcessSlice Previous { get; init; }

    /// <summary>Nodes added, removed or edited, in the order the current walk found them.</summary>
    public IReadOnlyList<ProcessChange> Changes { get; init; } = [];

    /// <summary>Steps that exist now and did not, rendered as "A → B".</summary>
    public IReadOnlyList<string> StepsAdded { get; init; } = [];

    /// <summary>Steps that existed and no longer do.</summary>
    public IReadOnlyList<string> StepsRemoved { get; init; } = [];

    /// <summary>Calls the walk cannot follow now and could before, and the reverse.</summary>
    public IReadOnlyList<string> BlindSpotsGained { get; init; } = [];

    /// <inheritdoc cref="BlindSpotsGained"/>
    public IReadOnlyList<string> BlindSpotsLost { get; init; } = [];

    /// <summary>Whether the process existed at all in the snapshot.</summary>
    public bool ExistedBefore => Previous.Found;

    /// <summary>Whether anything at all is different.</summary>
    public bool AnyChange =>
        Changes.Count > 0 || StepsAdded.Count > 0 || StepsRemoved.Count > 0
        || BlindSpotsGained.Count > 0 || BlindSpotsLost.Count > 0;

    /// <summary>Walks the same seed over both extractions and compares the results.</summary>
    public static WalkthroughDiff Between(
        ExtractedProject previous, ExtractedProject current, string seed, int depth = SliceWalker.DefaultDepth)
    {
        var before = ProcessSlice.From(previous, seed, depth);
        var after = ProcessSlice.From(current, seed, depth);

        var beforeNodes = before.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var afterNodes = after.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        var changes = new List<ProcessChange>();

        // Current order first, so the report reads in the order the process runs rather than
        // alphabetically or by the accident of which set was enumerated.
        foreach (var node in after.Nodes)
        {
            if (!beforeNodes.TryGetValue(node.Id, out var was))
                changes.Add(new ProcessChange(node, ProcessChangeKind.Added));
            else if (!string.Equals(was.Fingerprint, node.Fingerprint, StringComparison.Ordinal))
                changes.Add(new ProcessChange(node, ProcessChangeKind.Changed));
        }

        foreach (var node in before.Nodes.Where(node => !afterNodes.ContainsKey(node.Id)))
            changes.Add(new ProcessChange(node, ProcessChangeKind.Removed));

        return new WalkthroughDiff
        {
            Current = after,
            Previous = before,
            Changes = changes,
            StepsAdded = [.. Steps(after, afterNodes).Except(Steps(before, beforeNodes), StringComparer.Ordinal)],
            StepsRemoved = [.. Steps(before, beforeNodes).Except(Steps(after, afterNodes), StringComparer.Ordinal)],
            BlindSpotsGained = [.. Blind(after).Except(Blind(before), StringComparer.Ordinal)],
            BlindSpotsLost = [.. Blind(before).Except(Blind(after), StringComparer.Ordinal)],
        };
    }

    /// <summary>Steps as readable text, which is also what makes them comparable across two walks.</summary>
    private static List<string> Steps(ProcessSlice slice, Dictionary<string, SliceNode> nodes) =>
    [
        .. slice.Edges
            .Where(edge => nodes.ContainsKey(edge.From) && nodes.ContainsKey(edge.To))
            .Select(edge => $"{nodes[edge.From].Name} —{edge.Kind}→ {nodes[edge.To].Name}"),
    ];

    private static List<string> Blind(ProcessSlice slice) =>
        [.. slice.Unresolved.Select(call => call.CallName)];
}
