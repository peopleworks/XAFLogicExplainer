using System.Globalization;
using System.Text;
using Microsoft.Extensions.AI;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.CopilotSync.Ai;

/// <summary>
/// Writes prose over a walk that has already been computed.
/// </summary>
/// <remarks>
/// The model narrates; it does not discover. It receives the steps the walk found, in order, with
/// the code behind them, and is asked what each one means in business terms. It cannot add a step,
/// because the only thing that reaches the document is a line it managed to key to a step that
/// exists — anything else is dropped here, before it can be read.
/// <para>
/// That is the difference between prose over a structure and prose instead of one. A model asked to
/// explain "the approval process" from scratch will produce something fluent, complete-looking and
/// unverifiable. Given a fixed set of numbered steps, the worst it can do is describe one of them
/// badly, which a reader can see and correct.
/// </para>
/// <para>
/// Failure is graceful and silent in only one direction: no narration, structure intact. The
/// document is worth reading without a word of this.
/// </para>
/// </remarks>
public sealed class WalkthroughNarrator
{
    /// <summary>Longest method body sent for one step.</summary>
    private const int MaxBodyLength = 2000;

    private readonly IChatClient _chatClient;

    /// <summary>Creates the narrator over a resolved chat client.</summary>
    public WalkthroughNarrator(IChatClient chatClient) => _chatClient = chatClient;

    /// <summary>
    /// Returns prose keyed by step number, with <c>0</c> being the opening paragraph.
    /// </summary>
    /// <remarks>
    /// Empty when the model said nothing usable, which the caller renders as a document with no
    /// narration rather than as an error.
    /// </remarks>
    public async Task<IReadOnlyDictionary<int, string>> NarrateAsync(
        ExtractedProject project,
        ProcessSlice slice,
        string languageCode = "es",
        CancellationToken cancellationToken = default)
    {
        if (!slice.Found || slice.Edges.Count == 0)
            return new Dictionary<int, string>();

        var response = await _chatClient.GetResponseAsync(
            Prompt(project, slice, languageCode), cancellationToken: cancellationToken);

        return Parse(response.Text, slice.Edges.Count);
    }

    private static string Prompt(ExtractedProject project, ProcessSlice slice, string languageCode)
    {
        var language = languageCode == "en" ? "English" : "Spanish";
        var byId = slice.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var sb = new StringBuilder();

        sb.AppendLine($"You are documenting one business process in the XAF application \"{project.ProjectName}\".");
        sb.AppendLine();
        sb.AppendLine("A static analysis already traced the process. Below are its steps, in order, with the");
        sb.AppendLine("code behind them. Explain what each step means in business terms — what it is for, what");
        sb.AppendLine("it decides, what it would mean for a user if it were removed.");
        sb.AppendLine();
        sb.AppendLine("RULES, all of them strict:");
        sb.AppendLine("- Write one line per step, in this exact format: N| your sentence or two");
        sb.AppendLine("- N must be a step number from the list. Never invent a number.");
        sb.AppendLine("- Line 0 is one short paragraph on what the whole process is for.");
        sb.AppendLine("- Say nothing the code below does not support. No speculation about intent.");
        sb.AppendLine("- Skip a step you have nothing useful to say about. A missing line is fine.");
        sb.AppendLine("- No markdown, no bullet points, no headings, no code. Plain sentences.");
        sb.AppendLine($"- Write in {language}.");
        sb.AppendLine();
        sb.AppendLine($"PROCESS: {slice.Seed}");
        sb.AppendLine();

        var step = 0;

        foreach (var edge in slice.Edges)
        {
            if (!byId.TryGetValue(edge.From, out var from) || !byId.TryGetValue(edge.To, out var to))
                continue;

            sb.AppendLine($"{++step}. {from.Name} — {edge.Kind} → {to.Name} ({to.Kind})");

            if (Detail(project, to) is { Length: > 0 } detail)
                sb.AppendLine(detail);
        }

        if (slice.Unresolved.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("The analysis could not follow these calls, so do not claim to know what they do:");

            foreach (var call in slice.Unresolved)
                sb.AppendLine($"- {call.CallName}: {string.Join(", ", call.Candidates)}");
        }

        return sb.ToString();
    }

    /// <summary>The code or the declaration behind one node, for the model to read.</summary>
    private static string Detail(ExtractedProject project, SliceNode node)
    {
        switch (node.Kind)
        {
            case SliceNodeKind.Method when node.Owner is { Length: > 0 } owner:
            {
                var method = project.Controllers
                    .FirstOrDefault(controller => controller.ClassName == owner)?.Methods
                    .FirstOrDefault(m => $"{owner}.{m.Name}" == node.Name);

                return method is null || method.Body.Length == 0
                    ? ""
                    : "   ```\n   " + Cap(method.Body).Replace("\n", "\n   ", StringComparison.Ordinal) + "\n   ```";
            }

            case SliceNodeKind.Entity:
            {
                var entity = project.Entities.FirstOrDefault(e => e.ClassName == node.Name);

                return entity is null
                    ? ""
                    : "   properties: " + string.Join(", ",
                        entity.Properties.Take(15).Select(p => $"{p.Name} {p.TypeName}"));
            }

            case SliceNodeKind.ValidationRule or SliceNodeKind.AppearanceRule when node.Owner is { Length: > 0 } owner:
            {
                var entity = project.Entities.FirstOrDefault(e => e.ClassName == owner);
                var criteria = entity?.ValidationRules
                    .FirstOrDefault(rule => rule.Id == node.Name)?.TargetCriteria;

                return criteria is { Length: > 0 } ? $"   criteria: {criteria}" : "";
            }

            default:
                return "";
        }
    }

    private static string Cap(string code) =>
        code.Length <= MaxBodyLength ? code : code[..MaxBodyLength];

    /// <summary>
    /// Keeps the lines that name a step that exists, and drops everything else.
    /// </summary>
    /// <remarks>
    /// The enforcement, and the reason the model cannot widen the account it was given. A paragraph
    /// keyed to step 12 of an eight-step process is not a step this walk found, so whatever it says
    /// never reaches a reader — the point is not that the sentence is probably wrong, it is that
    /// nobody could check it.
    /// </remarks>
    private static Dictionary<int, string> Parse(string? text, int steps)
    {
        var narration = new Dictionary<int, string>();

        if (string.IsNullOrWhiteSpace(text))
            return narration;

        foreach (var line in text.Split('\n'))
        {
            var separator = line.IndexOf('|');

            if (separator <= 0)
                continue;

            if (!int.TryParse(line[..separator].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var step))
            {
                continue;
            }

            if (step < 0 || step > steps)
                continue;

            var prose = line[(separator + 1)..].Trim();

            // First line wins, so a model that repeats itself cannot append to a step.
            if (prose.Length > 0)
                narration.TryAdd(step, prose);
        }

        return narration;
    }
}
