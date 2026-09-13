using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// A laid-out picture of how an application's entities relate to each other.
/// </summary>
/// <remarks>
/// Most XAF teams have never seen their own domain model drawn. It exists as association
/// attributes scattered across twenty files, and the map of it lives in one person's head — which
/// is precisely the knowledge that leaves when they do.
/// <para>
/// The layout is computed here rather than in the browser: the output is a single file that has to
/// work offline, in an email attachment, and with no script blocked by a corporate policy. Being
/// deterministic also means the same source always draws the same diagram, so a regenerated
/// explainer produces a readable diff instead of a reshuffled one.
/// </para>
/// </remarks>
public sealed class EntityGraph
{
    private EntityGraph(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges, double width, double height)
    {
        Nodes = nodes;
        Edges = edges;
        Width = width;
        Height = height;
    }

    /// <summary>Entities, positioned.</summary>
    public IReadOnlyList<GraphNode> Nodes { get; }

    /// <summary>Relationships between them.</summary>
    public IReadOnlyList<GraphEdge> Edges { get; }

    /// <summary>Canvas width.</summary>
    public double Width { get; }

    /// <summary>Canvas height.</summary>
    public double Height { get; }

    /// <summary>True when there is nothing worth drawing.</summary>
    public bool IsEmpty => Nodes.Count == 0;

    /// <summary>
    /// Lays out an application's entities on a circle.
    /// </summary>
    /// <param name="project">The extracted application.</param>
    /// <param name="width">Canvas width, or null to size it to the number of entities.</param>
    /// <param name="height">Canvas height, or null to size it to the number of entities.</param>
    public static EntityGraph Build(ExtractedProject project, double? width = null, double? height = null)
    {
        // A fixed canvas makes three entities float in an ocean of white and twenty-five collide.
        // The ring has to grow with the count, and the canvas with the ring.
        var count = Math.Max(project.Entities.Count, 1);
        height ??= Math.Clamp(300 + count * 20, 340, 660);
        width ??= Math.Max(height.Value * 1.5, 560);

        // Classes, not names: two classes can share a name in two namespaces, and keying the
        // drawing by the name threw on exactly the application that needed the map most (#84).
        var directory = new EntityDirectory(project.Entities);

        // Only relationships between entities this application defines. A property pointing at a
        // framework type is real, but drawing it adds a node nobody can navigate to.
        var pairs = new List<(ExtractedEntity From, ExtractedEntity To, bool Aggregated, string Label)>();

        foreach (var entity in project.Entities)
        {
            // What the class declares. A base's association belongs to every descendant and is
            // listed under each, but the diagram is of the schema: one arrow from the class that
            // wrote it, not one from each class that received it.
            foreach (var relationship in entity.Relationships.Where(r => r.InheritedFrom is null))
            {
                // Read where it is written, so a bare name means the class in the declaring
                // namespace and a name with its namespace in front of it is still found.
                if (directory.Resolve(relationship.RelatedEntity, entity.Namespace) is not { } related)
                    continue;

                // An association appears on both ends. Keep the side that owns or that points to
                // many, so the arrow means something rather than being drawn twice.
                if (relationship.Type == RelationshipType.ManyToOne && !relationship.IsAggregated)
                    continue;

                pairs.Add((entity, related, relationship.IsAggregated, relationship.PropertyName));
            }
        }

        var degrees = new Dictionary<ExtractedEntity, int>(ReferenceEqualityComparer.Instance);
        foreach (var entity in project.Entities)
            degrees[entity] = 0;

        foreach (var (from, to, _, _) in pairs)
        {
            degrees[from]++;
            degrees[to]++;
        }

        var ordered = OrderToReduceCrossings(project.Entities, pairs, degrees);

        var canvasWidth = width.Value;
        var canvasHeight = height.Value;
        var centreX = canvasWidth / 2;
        var centreY = canvasHeight / 2;
        var ring = Math.Min(canvasWidth, canvasHeight) / 2 - 74;

        var nodes = new List<GraphNode>();
        var byEntity = new Dictionary<ExtractedEntity, GraphNode>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < ordered.Count; i++)
        {
            var entity = ordered[i];

            // Start at the top and go clockwise, so the first entity is where a reader looks first.
            var angle = (2 * Math.PI * i / ordered.Count) - (Math.PI / 2);

            // A single entity has no circle to sit on.
            var x = ordered.Count == 1 ? centreX : centreX + ring * Math.Cos(angle);
            var y = ordered.Count == 1 ? centreY : centreY + ring * Math.Sin(angle);

            var node = new GraphNode(
                Name: directory.Label(entity),
                Anchor: directory.Anchor(entity),
                X: Math.Round(x, 2),
                Y: Math.Round(y, 2),
                Radius: RadiusFor(entity.Properties.Count),
                PropertyCount: entity.Properties.Count,
                Degree: degrees.GetValueOrDefault(entity),
                Angle: angle);

            nodes.Add(node);
            byEntity[entity] = node;
        }

        var edges = pairs
            .Where(p => byEntity.ContainsKey(p.From) && byEntity.ContainsKey(p.To))
            .Select(p => new GraphEdge(byEntity[p.From], byEntity[p.To], p.Aggregated, p.Label))
            .ToList();

        return new EntityGraph(nodes, edges, canvasWidth, canvasHeight);
    }

    /// <summary>
    /// Orders entities around the circle so related ones sit near each other.
    /// </summary>
    /// <remarks>
    /// Alphabetical order scatters an association's two ends to opposite sides, and every chord
    /// then crosses the middle — the diagram becomes a ball of string at about eight entities.
    /// <para>
    /// The walk is breadth-first on purpose. Following one neighbour at a time leaves a hub after
    /// its first spoke and never returns, so its remaining children land halfway around the circle
    /// and their edges cross everything. Placing a node and then <em>all</em> of its neighbours
    /// keeps a hub and its spokes together, which is the shape most XAF domain models have: an
    /// order with its lines, a sale with its payments.
    /// </para>
    /// <para>
    /// A heuristic, not an optimum. The aim is legibility, not a minimal crossing count.
    /// </para>
    /// </remarks>
    private static List<ExtractedEntity> OrderToReduceCrossings(
        List<ExtractedEntity> entities,
        List<(ExtractedEntity From, ExtractedEntity To, bool Aggregated, string Label)> pairs,
        Dictionary<ExtractedEntity, int> degrees)
    {
        var neighbours = new Dictionary<ExtractedEntity, HashSet<ExtractedEntity>>(ReferenceEqualityComparer.Instance);

        foreach (var entity in entities)
            neighbours[entity] = new HashSet<ExtractedEntity>(ReferenceEqualityComparer.Instance);

        foreach (var (from, to, _, _) in pairs)
        {
            if (neighbours.TryGetValue(from, out var a)) a.Add(to);
            if (neighbours.TryGetValue(to, out var b)) b.Add(from);
        }

        var remaining = new HashSet<ExtractedEntity>(entities, ReferenceEqualityComparer.Instance);
        var order = new List<ExtractedEntity>();

        while (remaining.Count > 0)
        {
            // Start each connected group at its busiest entity, so the hub of a cluster anchors it.
            // Ties break alphabetically, then by namespace, to keep the layout stable across runs.
            var seed = Busiest(remaining, degrees).First();

            var queue = new Queue<ExtractedEntity>();
            queue.Enqueue(seed);
            remaining.Remove(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                order.Add(current);

                var children = Busiest(neighbours[current].Where(remaining.Contains), degrees).ToList();

                foreach (var child in children)
                {
                    remaining.Remove(child);
                    queue.Enqueue(child);
                }
            }
        }

        return order;
    }

    private static IOrderedEnumerable<ExtractedEntity> Busiest(
        IEnumerable<ExtractedEntity> entities,
        Dictionary<ExtractedEntity, int> degrees) =>
        entities
            .OrderByDescending(entity => degrees.GetValueOrDefault(entity))
            .ThenBy(entity => entity.ClassName, StringComparer.Ordinal)
            .ThenBy(entity => entity.Namespace, StringComparer.Ordinal);

    /// <summary>
    /// Sizes a node by how much it carries.
    /// </summary>
    /// <remarks>
    /// Compressed deliberately: a linear scale makes a forty-property entity dwarf everything and
    /// turns the diagram into one blob. The range only has to say "this one is substantial".
    /// </remarks>
    private static double RadiusFor(int propertyCount) =>
        Math.Round(Math.Clamp(16 + Math.Sqrt(Math.Max(propertyCount, 0)) * 3.4, 16, 34), 2);
}

/// <summary>One entity, placed.</summary>
/// <param name="Name">What the drawing prints: the class name, with as much of its namespace as tells it from another class of that name.</param>
/// <param name="Anchor">The entity's fragment identifier on the page, unique within the application.</param>
/// <param name="X">Centre X.</param>
/// <param name="Y">Centre Y.</param>
/// <param name="Radius">Drawn radius.</param>
/// <param name="PropertyCount">How many properties it declares.</param>
/// <param name="Degree">How many relationships touch it.</param>
/// <param name="Angle">Angle on the circle, in radians, used to place the label outward.</param>
public sealed record GraphNode(
    string Name,
    string Anchor,
    double X,
    double Y,
    double Radius,
    int PropertyCount,
    int Degree,
    double Angle);

/// <summary>One relationship, drawn.</summary>
/// <param name="From">Owning end.</param>
/// <param name="To">Related entity.</param>
/// <param name="IsAggregated">True when the owner's deletion takes the other with it.</param>
/// <param name="Label">The property that declares it.</param>
public sealed record GraphEdge(GraphNode From, GraphNode To, bool IsAggregated, string Label);
