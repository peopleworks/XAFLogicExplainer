using Microsoft.Extensions.AI;
using XafLogicExplainer.CopilotSync.Ai;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Walkthrough;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Prose written over a walk, and the rule that keeps it honest.
/// </summary>
/// <remarks>
/// The model narrates; it does not discover. It is handed the steps the walk found and asked what
/// each one means, and the only thing that reaches a reader is a paragraph it managed to key to a
/// step that exists. It cannot widen the account it was given.
/// <para>
/// The point of that rule is not that an invented step would probably be wrong. It is that nobody
/// could check it — an ordinary reader cannot tell a fluent sentence about real code from a fluent
/// sentence about code that is not there, which is the entire failure this feature is built to
/// avoid.
/// </para>
/// <para>
/// Answers come from a stub rather than a provider: these are assertions about what the narrator
/// accepts, and a real model would make them flaky without making them stronger.
/// </para>
/// </remarks>
public class WalkthroughNarrationTests
{
    /// <summary>A chat client that says exactly what a test tells it to.</summary>
    private sealed class Stub : IChatClient
    {
        private readonly string _answer;

        internal Stub(string answer) => _answer = answer;

        internal string? LastPrompt { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastPrompt = string.Join("\n", messages.Select(message => message.Text));

            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, _answer)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static ProcessSlice Slice => ProcessSlice.From(SampleProjects.Xpo, "ApproveOrder");

    private static async Task<IReadOnlyDictionary<int, string>> Narrate(string answer) =>
        await new WalkthroughNarrator(new Stub(answer)).NarrateAsync(
            SampleProjects.Xpo, Slice, "en", TestContext.Current.CancellationToken);

    [Fact]
    public async Task AParagraphKeyedToARealStepIsKept()
    {
        var narration = await Narrate("0| Approving an order locks it.\n2| The handler refuses a blocked customer.");

        Assert.Equal("Approving an order locks it.", narration[0]);
        Assert.Equal("The handler refuses a blocked customer.", narration[2]);
    }

    [Fact]
    public async Task AParagraphKeyedToAStepThatDoesNotExistNeverReachesAReader()
    {
        // The enforcement. Step 99 is not a step this walk found, so whatever it says is a claim
        // about a process nobody can check against anything.
        var narration = await Narrate("2| A real step.\n99| A confident sentence about nothing.");

        Assert.True(narration.ContainsKey(2));
        Assert.False(narration.ContainsKey(99));
        Assert.DoesNotContain(narration.Values, prose => prose.Contains("nothing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProseWithNoStepAtAllIsDropped()
    {
        // What a model does when it ignores the format: a fluent paragraph attached to nothing. It
        // is the most likely failure and the least visible one, so it has to fall out here.
        var narration = await Narrate(
            "Here is a summary of the approval process, which is important for the business.\n"
            + "1| The controller declares the button.");

        var kept = Assert.Single(narration);

        Assert.Equal(1, kept.Key);
    }

    [Fact]
    public async Task ARepeatedStepKeepsTheFirstAnswer()
    {
        var narration = await Narrate("1| First.\n1| Second, contradicting the first.");

        Assert.Equal("First.", narration[1]);
    }

    [Fact]
    public async Task NothingUsableIsAnEmptyNarrationRatherThanAnError()
    {
        // The document is worth reading with no prose in it, so a model that says nothing costs
        // sentences and not the walkthrough.
        Assert.Empty(await Narrate(""));
        Assert.Empty(await Narrate("I'm sorry, I can't help with that."));
    }

    [Fact]
    public async Task TheModelIsToldWhatTheWalkCouldNotFollow()
    {
        // So it does not narrate its way over the gap. The one place a fluent sentence would do the
        // most damage is exactly where the analysis already knows it is blind.
        var stub = new Stub("");
        var slice = ProcessSlice.From(SampleProjects.Walkthrough, "RecalculateTotals");

        await new WalkthroughNarrator(stub).NarrateAsync(
            SampleProjects.Walkthrough, slice, "en", TestContext.Current.CancellationToken);

        Assert.Contains("could not follow these calls", stub.LastPrompt!, StringComparison.Ordinal);
        Assert.Contains("InvoiceTotalsController.Recalculate", stub.LastPrompt!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheModelIsGivenTheCodeRatherThanAskedToRememberIt()
    {
        var stub = new Stub("");

        await new WalkthroughNarrator(stub).NarrateAsync(
            SampleProjects.Xpo, Slice, "en", TestContext.Current.CancellationToken);

        Assert.Contains("A blocked customer cannot have orders approved", stub.LastPrompt!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProseLandsUnderTheStepItExplains()
    {
        // Where the citation and the sentence stop being able to come apart.
        var slice = Slice;
        var narration = await Narrate("2| It loads the order currently on screen.");
        var document = new WalkthroughGenerator("en").Generate(SampleProjects.Xpo, slice, narration).Replace("\r", "");

        var lines = document.Split('\n');
        var step = Array.FindIndex(lines, line => line.StartsWith("2. ", StringComparison.Ordinal));

        Assert.True(step >= 0);
        Assert.Contains("It loads the order currently on screen.", lines[step + 2], StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentWithNoNarrationIsUnchanged()
    {
        // The whole of phases 1 and 2 still stands on its own, which is what makes the AI optional
        // rather than load-bearing.
        var slice = Slice;

        Assert.Equal(
            new WalkthroughGenerator("en").Generate(SampleProjects.Xpo, slice),
            new WalkthroughGenerator("en").Generate(SampleProjects.Xpo, slice, new Dictionary<int, string>()));
    }
}
