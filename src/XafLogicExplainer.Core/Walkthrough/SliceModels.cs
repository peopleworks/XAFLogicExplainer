using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Walkthrough;

/// <summary>What one node in a slice is.</summary>
public enum SliceNodeKind
{
    /// <summary>An action a user can invoke.</summary>
    Action,

    /// <summary>A controller method.</summary>
    Method,

    /// <summary>A controller.</summary>
    Controller,

    /// <summary>A business entity.</summary>
    Entity,

    /// <summary>A validation rule carried by an entity.</summary>
    ValidationRule,

    /// <summary>An appearance rule carried by an entity.</summary>
    AppearanceRule,
}

/// <summary>How one node reaches another.</summary>
public enum SliceEdgeKind
{
    /// <summary>A method or an action's handler invokes a method the project declares.</summary>
    Calls,

    /// <summary>Code names an entity — creates it, casts to it, reads it.</summary>
    Touches,

    /// <summary>A controller declares an action.</summary>
    Declares,

    /// <summary>A controller is activated for an entity.</summary>
    Targets,

    /// <summary>An entity carries a rule.</summary>
    Governs,
}

/// <summary>
/// One thing the walk found, and where to read it.
/// </summary>
public sealed record SliceNode
{
    /// <summary>Stable key, unique within a slice.</summary>
    public required string Id { get; init; }

    /// <summary>What kind of thing this is.</summary>
    public required SliceNodeKind Kind { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>The controller or entity this belongs to, when it belongs to one.</summary>
    public string? Owner { get; init; }

    /// <summary>Source file, when known.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>One-based line, or zero when there is none to give.</summary>
    public int Line { get; init; }

    /// <summary>Hops from the seed. The seed itself is zero.</summary>
    public int Distance { get; init; }
}

/// <summary>One resolved step between two nodes.</summary>
/// <param name="From">Id of the node the step leaves.</param>
/// <param name="To">Id of the node the step reaches.</param>
/// <param name="Kind">What kind of step it is.</param>
public sealed record SliceEdge(string From, string To, SliceEdgeKind Kind);

/// <summary>
/// A call the walk could see but could not follow, and what it might have reached.
/// </summary>
/// <remarks>
/// Printed rather than dropped. A walkthrough that quietly stops at a call it cannot resolve would
/// be the first output in this project that lies by omission — it would read as a complete account
/// of a process while a branch of that process was simply missing.
/// </remarks>
public sealed record UnresolvedCall
{
    /// <summary>Id of the node whose code makes the call.</summary>
    public required string From { get; init; }

    /// <summary>The name being invoked.</summary>
    public required string CallName { get; init; }

    /// <summary>Every declaration the name could mean, named so a reader can choose.</summary>
    public required IReadOnlyList<string> Candidates { get; init; }

    /// <summary>Why syntax alone cannot decide.</summary>
    public required string Reason { get; init; }
}

/// <summary>
/// One business process, as far as syntax can follow it.
/// </summary>
/// <remarks>
/// The scope is decided here, by a bounded walk, and not by a language model. A model asked what
/// belongs in "the commission process" produces something authoritative, checkable by nobody, and
/// wrong in places that look exactly like the places it is right. A walk produces a scope that can
/// be reviewed, that diffs between two extractions, and whose bad sentences can be fixed without
/// touching its structure.
/// </remarks>
public sealed record ProcessSlice
{
    /// <summary>What the caller asked for.</summary>
    public required string Seed { get; init; }

    /// <summary>The node the walk started from, when the seed matched something.</summary>
    public SliceNode? Root { get; init; }

    /// <summary>Why there is no root, when there is none.</summary>
    public string? Problem { get; init; }

    /// <summary>The hop limit the walk was given.</summary>
    public int Depth { get; init; }

    /// <summary>
    /// Whether the walk stopped at the limit rather than running out of things to reach.
    /// </summary>
    /// <remarks>
    /// Reported because it changes what the slice means: a walk that ran out is a whole process, and
    /// a walk that hit the bound is a view of one. Presenting them identically is how a document
    /// comes to claim completeness it does not have.
    /// </remarks>
    public bool DepthReached { get; init; }

    /// <summary>Everything reached, nearest to the seed first.</summary>
    public IReadOnlyList<SliceNode> Nodes { get; init; } = [];

    /// <summary>Every step between them.</summary>
    public IReadOnlyList<SliceEdge> Edges { get; init; } = [];

    /// <summary>Calls the walk saw and could not follow.</summary>
    public IReadOnlyList<UnresolvedCall> Unresolved { get; init; } = [];

    /// <summary>Whether the seed matched something.</summary>
    public bool Found => Root is not null;

    /// <summary>
    /// Walks one process out of an extracted project.
    /// </summary>
    /// <param name="project">The application to walk.</param>
    /// <param name="seed">An action, a controller method, a controller or an entity, by name.</param>
    /// <param name="depth">How many hops from the seed to follow.</param>
    public static ProcessSlice From(ExtractedProject project, string seed, int depth = SliceWalker.DefaultDepth) =>
        SliceWalker.Walk(new SliceIndex(project), seed, depth);
}
