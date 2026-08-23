using System.ComponentModel;
using ModelContextProtocol.Server;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Mcp.Tools;

/// <summary>
/// The tool that answers a question about a process rather than about a declaration.
/// </summary>
/// <remarks>
/// Every other tool here returns an atom: one entity, one controller, one view. An agent asked how
/// commission gets calculated has to guess which atoms to fetch, and then guess whether it has them
/// all — which is the failure this whole feature exists for, because the guess that stops one atom
/// early produces a confident answer with a step missing from it.
/// <para>
/// The walk decides the scope instead, and reports what it could not follow. An agent that receives
/// "this call is virtual, and here are the three overrides" can go and read all three; an agent that
/// receives silence cannot know to.
/// </para>
/// </remarks>
[McpServerToolType]
public sealed class XafWalkthroughTools
{
    private readonly XafProjectContext _context;

    /// <summary>Creates the tool set.</summary>
    public XafWalkthroughTools(XafProjectContext context) => _context = context;

    /// <summary>Traces one process from a seed the caller names.</summary>
    [McpServerTool(Name = "xaf_walkthrough")]
    [Description(
        "Trace one business process end to end: what runs, in what order, which entities it " +
        "touches and which rules govern them, every step citing file and line. Start from an " +
        "action, a controller method, a controller or an entity. Use this when asked HOW " +
        "something works — 'how does approval work', 'what happens when I press this' — rather " +
        "than what exists. Calls the trace could not follow are reported explicitly, so an " +
        "empty list of them means the path really is complete.")]
    public async Task<string> WalkthroughAsync(
        [Description("Where to start: an action, a controller method, a controller or an entity.")]
        string from,
        [Description("How many hops to follow. Three covers an ordinary XAF process.")]
        int depth = 3,
        [Description("Project name, when several are configured.")]
        string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);
        var slice = ProcessSlice.From(app, from, depth);

        // English regardless of the project's documentation language: this text is read by an agent
        // reasoning in the conversation's language, not filed as a deliverable.
        return new WalkthroughGenerator("en").Generate(app, slice);
    }
}
