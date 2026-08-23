using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XafLogicExplainer.Core.Walkthrough;

/// <summary>
/// Walks a project from one seed and collects the process around it.
/// </summary>
/// <remarks>
/// Breadth-first, bounded by depth, and syntax-only like everything else here: a call is matched by
/// name against the methods the project declares, with no compilation and no symbol table. What that
/// cannot decide is reported rather than guessed — see <see cref="UnresolvedCall"/>.
/// <para>
/// The reach of a slice is the reach of the extraction. Methods are extracted from controllers, so a
/// calculation that lives in a plain service class is not walked into. Entity-to-entity
/// relationships are deliberately not followed either: they are what makes a slice reach the whole
/// application, and <c>EntityGraph</c> already exists for that question.
/// </para>
/// </remarks>
internal static class SliceWalker
{
    /// <summary>
    /// Hops from the seed by default.
    /// </summary>
    /// <remarks>
    /// Three reaches action → handler → the entity it writes and the methods it calls, which is the
    /// shape of an ordinary XAF process. In a real application everything eventually reaches
    /// everything, so this is a choice about usefulness rather than a technical limit.
    /// </remarks>
    internal const int DefaultDepth = 3;

    internal static ProcessSlice Walk(SliceIndex index, string seed, int depth)
    {
        if (depth < 0) depth = 0;

        var root = index.Resolve(seed);

        if (root is null)
            return new ProcessSlice { Seed = seed, Depth = depth, Problem = index.NothingMatched(seed) };

        var nodes = new Dictionary<string, SliceNode>(StringComparer.Ordinal) { [root.Node.Id] = root.Node };
        var edges = new List<SliceEdge>();
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new List<UnresolvedCall>();
        var seenUnresolved = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<SliceItem>();
        var depthReached = false;

        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var next = current.Node.Distance + 1;

            foreach (var (neighbour, kind, incoming) in Expand(
                current, root.Node.Id, index, unresolved, seenUnresolved))
            {
                if (neighbour.Node.Id == current.Node.Id)
                    continue;

                if (next > depth)
                {
                    // The bound, not the end of the process. Recorded so the document can say which
                    // of the two the reader is looking at.
                    depthReached = true;
                    continue;
                }

                if (!nodes.ContainsKey(neighbour.Node.Id))
                {
                    var placed = neighbour.AtDistance(next);
                    nodes[placed.Node.Id] = placed.Node;
                    queue.Enqueue(placed);
                }

                // Reaching a node and pointing at it are different things. An action is found by
                // walking up to its controller, but the fact is that the controller declares the
                // action -- and phase 2 draws arrows straight off this set, so an edge written
                // backwards would put an arrow in a diagram that the code does not support.
                var edge = incoming
                    ? new SliceEdge(neighbour.Node.Id, current.Node.Id, kind)
                    : new SliceEdge(current.Node.Id, neighbour.Node.Id, kind);

                if (seenEdges.Add($"{edge.From}|{edge.To}|{edge.Kind}"))
                    edges.Add(edge);
            }
        }

        return new ProcessSlice
        {
            Seed = seed,
            Root = root.Node,
            Depth = depth,
            DepthReached = depthReached,
            Nodes = [.. nodes.Values.OrderBy(n => n.Distance).ThenBy(n => n.Id, StringComparer.Ordinal)],
            Edges = edges,
            Unresolved = unresolved,
        };
    }

    /// <summary>Everything one node reaches in a single hop.</summary>
    private static IEnumerable<(SliceItem Node, SliceEdgeKind Kind, bool Incoming)> Expand(
        SliceItem item,
        string rootId,
        SliceIndex index,
        List<UnresolvedCall> unresolved,
        HashSet<string> seenUnresolved)
    {
        switch (item.Payload)
        {
            case ActionRef reference:
            {
                // Incoming: the controller declares the action, not the other way round.
                yield return (index.Controller(reference.Controller), SliceEdgeKind.Declares, true);

                if (reference.Action.ExecuteMethodName is { Length: > 0 } handler
                    && index.Method(reference.Controller, handler) is { } handlerNode)
                {
                    // And nothing else. An action's extracted body *is* its handler's body, so
                    // walking both would give the action a copy of every edge the handler has --
                    // two nodes reporting one call, and a diagram that forks where the code does
                    // not. The handler is the node that runs the code, so it is the one that walks.
                    yield return (handlerNode, SliceEdgeKind.Calls, false);
                    break;
                }

                // No handler to hand off to: a lambda subscribed inline, or a body that extraction
                // was told not to keep. The action walks its own code rather than stopping.
                foreach (var reached in FromCode(
                    item, reference.Action.ExecuteMethodBody, reference.Controller,
                    index, unresolved, seenUnresolved))
                {
                    yield return reached;
                }

                break;
            }

            case MethodRef reference:
            {
                foreach (var reached in FromCode(
                    item, reference.Method.Body, reference.Controller, index, unresolved, seenUnresolved))
                {
                    yield return reached;
                }

                break;
            }

            case ControllerRef reference:
            {
                foreach (var entity in index.EntitiesOf(reference.Controller))
                    yield return (entity, SliceEdgeKind.Targets, false);

                // Only when the controller is what was asked for. Arriving at it from one of its own
                // actions and then fanning out to its siblings answers a question nobody asked: a
                // walkthrough of "what happens when I press Approve" does not want the other buttons.
                if (item.Node.Id == rootId)
                {
                    foreach (var action in reference.Controller.Actions)
                        yield return (index.Action(reference.Controller, action), SliceEdgeKind.Declares, false);

                    // Its own methods too, and only here. Reached from an action, a controller
                    // contributes what that action runs; asked about directly, it is the subject,
                    // and a helper nothing happens to call is still part of what it declares.
                    foreach (var method in reference.Controller.Methods)
                        yield return (index.Method(reference.Controller, method), SliceEdgeKind.Declares, false);
                }

                break;
            }

            case EntityRef reference:
            {
                foreach (var rule in index.RulesOf(reference.Entity))
                    yield return (rule, SliceEdgeKind.Governs, false);

                break;
            }
        }
    }

    /// <summary>
    /// What a body of code reaches: the methods it calls, and the entities it names.
    /// </summary>
    private static IEnumerable<(SliceItem Node, SliceEdgeKind Kind, bool Incoming)> FromCode(
        SliceItem from,
        string? body,
        Models.ExtractedController owner,
        SliceIndex index,
        List<UnresolvedCall> unresolved,
        HashSet<string> seenUnresolved)
    {
        if (Parse(body) is not { } code)
            yield break;

        foreach (var invocation in code.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var (name, qualified) = CalledName(invocation);

            if (name is null)
                continue;

            // An unqualified call resolves against the calling class first, which is what C# itself
            // does — otherwise two controllers that each declare `Validate()` would make every call
            // to either one ambiguous.
            if (!qualified && index.Method(owner, name) is { } local)
            {
                yield return (local, SliceEdgeKind.Calls, false);

                // The declaration written beside the call is the one the walk can point at. It is
                // not necessarily the one that runs: a virtual method is replaced by whichever
                // override the activated controller carries, and nothing in the source says which.
                // Following the edge and saying nothing would be the quiet stop this must not make.
                if (local.Payload is MethodRef { Method.IsOverridable: true })
                {
                    var replacements = index.MethodsNamed(name)
                        .Where(candidate => candidate.Node.Id != local.Node.Id)
                        .ToList();

                    if (replacements.Count > 0 && seenUnresolved.Add($"{from.Node.Id}|{name}"))
                    {
                        unresolved.Add(new UnresolvedCall
                        {
                            From = from.Node.Id,
                            CallName = name,
                            Candidates = [local.Node.Name, .. replacements.Select(r => r.Node.Name)],
                            Reason = "the declaration is virtual; which override runs is decided by "
                                     + "the run-time type, not by the source",
                        });
                    }
                }

                continue;
            }

            var candidates = index.MethodsNamed(name);

            if (candidates.Count == 1)
            {
                yield return (candidates[0], SliceEdgeKind.Calls, false);
            }
            else if (candidates.Count > 1)
            {
                // The shape a virtual call takes in syntax: a base declaration and its overrides all
                // carry one name, and which of them runs is decided at run time.
                if (seenUnresolved.Add($"{from.Node.Id}|{name}"))
                {
                    unresolved.Add(new UnresolvedCall
                    {
                        From = from.Node.Id,
                        CallName = name,
                        Candidates = [.. candidates.Select(candidate => candidate.Node.Name)],
                        Reason = "several declarations carry this name; syntax alone cannot say which one runs",
                    });
                }
            }

            // Nothing declares it: the framework, the base class library, or a type outside the
            // extraction. Reporting every `CommitChanges()` would bury the calls that genuinely are
            // ambiguous, so those are left silent by design.
        }

        var (typeNames, memberNames) = Names(code);

        foreach (var named in typeNames)
        {
            if (index.Entity(named) is { } entity)
                yield return (entity, SliceEdgeKind.Touches, false);
        }

        // `order.Customer` names an entity too, but not where a type sits -- and matching a property
        // name against the class names is right here by luck and wrong the first time an entity is
        // called `Status`. So a name written after a dot counts only when the model declares a
        // relationship of that name pointing at that entity.
        foreach (var named in memberNames)
        {
            foreach (var entity in index.EntitiesNavigatedBy(named))
                yield return (entity, SliceEdgeKind.Touches, false);
        }
    }

    /// <summary>Parses an extracted body back into syntax, block or expression alike.</summary>
    /// <remarks>
    /// Roslyn recovers from what it cannot parse rather than refusing, which is the behaviour wanted
    /// here: a body it only half understands still yields the calls and the type names it did.
    /// </remarks>
    private static SyntaxNode? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        var text = body.Trim();

        // An expression-bodied member is extracted with its arrow, which is not a statement.
        if (text.StartsWith("=>", StringComparison.Ordinal))
            text = "{ " + text[2..].Trim().TrimEnd(';') + "; }";

        return SyntaxFactory.ParseStatement(text);
    }

    /// <summary>The method name an invocation names, and whether it was reached through something.</summary>
    private static (string? Name, bool Qualified) CalledName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => (identifier.Identifier.Text, false),
            GenericNameSyntax generic => (generic.Identifier.Text, false),
            MemberAccessExpressionSyntax member => (member.Name.Identifier.Text, true),
            MemberBindingExpressionSyntax binding => (binding.Name.Identifier.Text, true),
            _ => (null, false),
        };

    /// <summary>
    /// The names a body uses, split by whether they sit where a type sits.
    /// </summary>
    /// <remarks>
    /// The type half covers the three ways an XAF handler names its entity: a cast, a declaration,
    /// and a generic argument such as <c>CreateObject&lt;Order&gt;()</c>. The member half is
    /// everything written after a dot, which is a property or a method rather than a type, and is
    /// resolved against the model's relationships instead of against its class names.
    /// </remarks>
    private static (List<string> Types, List<string> Members) Names(SyntaxNode code)
    {
        var accessed = code.DescendantNodes().OfType<MemberAccessExpressionSyntax>().ToList();
        var memberNodes = accessed.Select(access => access.Name).ToHashSet();
        var members = new HashSet<string>(StringComparer.Ordinal);

        foreach (var access in accessed)
            members.Add(access.Name.Identifier.Text);

        var types = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in code.DescendantNodes())
        {
            if (node is not SimpleNameSyntax name || memberNodes.Contains(name))
                continue;

            if (seen.Add(name.Identifier.Text))
                types.Add(name.Identifier.Text);
        }

        return (types, [.. members]);
    }
}
