using XafLogicExplainer.Core.Catalog;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// The corpus laid out as pictures rather than as lists.
/// </summary>
/// <remarks>
/// Positions are computed here rather than in the generator so the arithmetic can be tested without
/// parsing HTML, and so the drawing code stays drawing code. Nothing is random: the same corpus lays
/// out identically every run, because a diagram that moves between two runs of the same tool is a
/// diagram nobody trusts to compare.
/// </remarks>
public static class CorpusGraph
{
    // ------------------------------------------------------------------
    // Which applications resemble which
    // ------------------------------------------------------------------

    /// <summary>
    /// Counts, for every pair of applications, how many class names they both model.
    /// </summary>
    /// <remarks>
    /// The question this answers is not in any list: <em>which two of my projects are most alike?</em>
    /// It stays readable at twenty applications, where a map of edges would not.
    /// </remarks>
    public static OverlapGrid Overlap(WikiCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var names = corpus.Applications
            .Select(a => a.Project.Entities
                .Select(e => e.ClassName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.Ordinal))
            .ToList();

        var cells = new List<OverlapCell>();
        var highest = 0;

        for (var row = 0; row < corpus.Applications.Count; row++)
        {
            for (var column = 0; column < corpus.Applications.Count; column++)
            {
                var shared = row == column
                    ? names[row].Count
                    : names[row].Count(n => names[column].Contains(n));

                if (row != column)
                    highest = Math.Max(highest, shared);

                cells.Add(new OverlapCell
                {
                    Row = row,
                    Column = column,
                    RowSlug = corpus.Applications[row].Slug,
                    ColumnSlug = corpus.Applications[column].Slug,
                    Shared = shared,
                    IsSelf = row == column,
                });
            }
        }

        return new OverlapGrid
        {
            Applications = corpus.Applications.Select(a => (a.Name, a.Slug)).ToList(),
            Cells = cells,
            Highest = highest,
        };
    }

    // ------------------------------------------------------------------
    // What sits between which applications
    // ------------------------------------------------------------------

    /// <summary>
    /// Places the applications on a ring and every shared class between the ones that model it.
    /// </summary>
    /// <remarks>
    /// A class is placed at the average direction of the applications that have it, at a radius set
    /// by how <em>agreed</em> that direction is. Two neighbours sharing a class put it between them;
    /// a class every application models has no direction at all and falls to the centre. So the
    /// picture reads without a caption: the middle is your common ground, and the rim is the work
    /// that belongs to one client.
    /// </remarks>
    public static CorpusMap Map(WikiCorpus corpus, double width = 940, double height = 500)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var count = corpus.Applications.Count;

        if (count < 2 || corpus.RecurringEntities.Count == 0)
            return new CorpusMap { Width = width, Height = height };

        var centreX = width / 2;
        var centreY = height / 2;
        // Room outside the ring for the labels, which sit further out than the nodes.
        var ring = Math.Min(width, height) / 2 - 62;

        var biggest = Math.Max(1, corpus.Applications.Max(a => a.Project.Entities.Count));
        var applications = new List<MapApplication>();
        var angles = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var index = 0; index < count; index++)
        {
            var application = corpus.Applications[index];
            // Starting at the top and going clockwise, so the reading order matches the nav.
            var angle = (-Math.PI / 2) + (2 * Math.PI * index / count);

            angles[application.Slug] = angle;

            applications.Add(new MapApplication
            {
                Name = application.Name,
                Slug = application.Slug,
                Angle = angle,
                X = centreX + (Math.Cos(angle) * ring),
                Y = centreY + (Math.Sin(angle) * ring),
                // Area, not radius, follows the entity count: doubling a radius quadruples the ink.
                Radius = 11 + (13 * Math.Sqrt((double)application.Project.Entities.Count / biggest)),
                EntityCount = application.Project.Entities.Count,
            });
        }

        var classes = new List<MapClass>();
        var links = new List<MapLink>();

        foreach (var recurring in corpus.RecurringEntities)
        {
            var slugs = recurring.In.Select(s => s.Slug).Distinct(StringComparer.Ordinal).ToList();

            var sumX = slugs.Sum(s => Math.Cos(angles[s]));
            var sumY = slugs.Sum(s => Math.Sin(angles[s]));

            // How much the applications holding this class agree on a direction. One is 1: they are
            // all in the same place. Zero: they are spread evenly, so there is no "between".
            var agreement = Math.Sqrt((sumX * sumX) + (sumY * sumY)) / slugs.Count;
            var direction = Math.Atan2(sumY, sumX);
            var radius = ring * 0.66 * agreement;

            classes.Add(new MapClass
            {
                ClassName = recurring.ClassName,
                Slugs = slugs,
                X = centreX + (Math.Cos(direction) * radius),
                Y = centreY + (Math.Sin(direction) * radius),
                Radius = 4 + (2.1 * slugs.Count),
                Direction = direction,
            });

            foreach (var slug in slugs)
                links.Add(new MapLink { Slug = slug, ClassName = recurring.ClassName });
        }

        return new CorpusMap
        {
            Width = width,
            Height = height,
            Applications = applications,
            Classes = Separate(classes),
            Links = links,
        };
    }

    /// <summary>
    /// Nudges classes that landed on the same spot onto a small ring around it.
    /// </summary>
    /// <remarks>
    /// Classes shared by exactly the same applications get exactly the same position, which is
    /// correct and unreadable — five circles drawn on top of each other look like one. Fanning them
    /// out by their order in the list keeps the result deterministic.
    /// </remarks>
    private static List<MapClass> Separate(List<MapClass> classes)
    {
        // A coarse cell, because the collision that ruins the picture is between labels rather than
        // between circles: two names an inch apart still overprint, and a name half-drawn over
        // another is worse than no name.
        var groups = classes
            .GroupBy(c => (Math.Round(c.X / 26), Math.Round(c.Y / 26)))
            .ToList();

        var separated = new List<MapClass>();

        foreach (var group in groups)
        {
            var members = group.ToList();

            if (members.Count == 1)
            {
                var only = members[0];

                separated.Add(only with
                {
                    LabelX = only.X,
                    LabelY = only.Y - only.Radius - 5,
                    LabelAnchor = "middle",
                });

                continue;
            }

            // Wide enough that the names clear each other, not just the circles. A cluster is
            // where the interesting classes are, so it is the one place the drawing must not
            // become a smudge.
            var spread = 26 + (5.5 * members.Count);

            for (var index = 0; index < members.Count; index++)
            {
                var angle = 2 * Math.PI * index / members.Count;
                var member = members[index];
                var x = member.X + (Math.Cos(angle) * spread);
                var y = member.Y + (Math.Sin(angle) * spread);

                // Each name is pushed away from the middle of its own cluster, the same way the
                // application names are pushed off the ring, so two neighbours lean apart.
                separated.Add(member with
                {
                    X = x,
                    Y = y,
                    LabelX = x + (Math.Cos(angle) * (member.Radius + 6)),
                    LabelY = y + (Math.Sin(angle) * (member.Radius + 6)) + (Math.Sin(angle) < -0.4 ? -3 : 9),
                    LabelAnchor = Math.Cos(angle) < -0.25 ? "end" : Math.Cos(angle) > 0.25 ? "start" : "middle",
                });
            }
        }

        // Back into the order they arrived in, so the SVG is stable between runs.
        return classes.Select(c => separated.First(s => s.ClassName == c.ClassName)).ToList();
    }

    // ------------------------------------------------------------------
    // How far apart the estate is
    // ------------------------------------------------------------------

    /// <summary>
    /// Lays the applications out along the DevExpress versions they declare.
    /// </summary>
    /// <remarks>
    /// Ordinal spacing, not numeric: a corpus holding 17.1 and 26.1 laid out to scale is one dot,
    /// a gap of nine years, and a cluster nobody can read. What the strip is for is the shape of the
    /// spread and which applications sit at the old end, and ordinal spacing shows both.
    /// </remarks>
    public static VersionSpread Versions(WikiCorpus corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var placed = corpus.Applications
            .Select(a => (a.Name, a.Slug, Version: DeclaredDevExpressVersion.Of(a.Project)))
            .Where(a => !string.IsNullOrWhiteSpace(a.Version))
            .ToList();

        var stops = placed
            .Select(a => a.Version!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(Numeric)
            .ToList();

        return new VersionSpread
        {
            Stops = stops,
            Applications = placed
                .Select(a => new VersionMark
                {
                    Name = a.Name,
                    Slug = a.Slug,
                    Version = a.Version!,
                    Stop = stops.IndexOf(a.Version!),
                })
                .OrderBy(m => m.Stop)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CatalogVersion = corpus.Applications
                .Select(a => a.Project.CatalogVersion)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            Undeclared = corpus.Applications.Count - placed.Count,
        };
    }

    private static (int Major, int Minor) Numeric(string version)
    {
        var parts = version.Split('.');

        return (parts.Length > 0 && int.TryParse(parts[0], out var major) ? major : 0,
                parts.Length > 1 && int.TryParse(parts[1], out var minor) ? minor : 0);
    }
}

/// <summary>How many class names each pair of applications has in common.</summary>
public sealed class OverlapGrid
{
    /// <summary>The applications, in order, along both axes.</summary>
    public IReadOnlyList<(string Name, string Slug)> Applications { get; init; } = [];

    /// <summary>Every cell, row-major.</summary>
    public IReadOnlyList<OverlapCell> Cells { get; init; } = [];

    /// <summary>The largest overlap between two different applications, for shading.</summary>
    public int Highest { get; init; }

    /// <summary>True when no two applications share anything.</summary>
    public bool IsEmpty => Highest == 0;
}

/// <summary>One pair of applications.</summary>
public sealed class OverlapCell
{
    /// <summary>Row index.</summary>
    public int Row { get; init; }

    /// <summary>Column index.</summary>
    public int Column { get; init; }

    /// <summary>Anchor slug of the row application.</summary>
    public string RowSlug { get; init; } = string.Empty;

    /// <summary>Anchor slug of the column application.</summary>
    public string ColumnSlug { get; init; } = string.Empty;

    /// <summary>Class names both model, or the entity count on the diagonal.</summary>
    public int Shared { get; init; }

    /// <summary>True on the diagonal, where the number means something else.</summary>
    public bool IsSelf { get; init; }
}

/// <summary>The applications and the classes that sit between them.</summary>
public sealed class CorpusMap
{
    /// <summary>Drawing width.</summary>
    public double Width { get; init; }

    /// <summary>Drawing height.</summary>
    public double Height { get; init; }

    /// <summary>The applications, on the ring.</summary>
    public IReadOnlyList<MapApplication> Applications { get; init; } = [];

    /// <summary>The classes more than one application models.</summary>
    public IReadOnlyList<MapClass> Classes { get; init; } = [];

    /// <summary>Which application models which class.</summary>
    public IReadOnlyList<MapLink> Links { get; init; } = [];

    /// <summary>True when there is nothing to draw.</summary>
    public bool IsEmpty => Classes.Count == 0;
}

/// <summary>One application on the ring.</summary>
public sealed record MapApplication
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Anchor slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Position on the ring, in radians.</summary>
    public double Angle { get; init; }

    /// <summary>Horizontal position.</summary>
    public double X { get; init; }

    /// <summary>Vertical position.</summary>
    public double Y { get; init; }

    /// <summary>Drawn radius, from the entity count.</summary>
    public double Radius { get; init; }

    /// <summary>Entities this application models.</summary>
    public int EntityCount { get; init; }
}

/// <summary>One class more than one application models.</summary>
public sealed record MapClass
{
    /// <summary>The class name.</summary>
    public required string ClassName { get; init; }

    /// <summary>The applications that model it.</summary>
    public required IReadOnlyList<string> Slugs { get; init; }

    /// <summary>Horizontal position.</summary>
    public double X { get; init; }

    /// <summary>Vertical position.</summary>
    public double Y { get; init; }

    /// <summary>Drawn radius, from how many applications model it.</summary>
    public double Radius { get; init; }

    /// <summary>The average direction of the applications that model it.</summary>
    public double Direction { get; init; }

    /// <summary>Where the name is drawn, kept clear of its neighbours.</summary>
    public double LabelX { get; init; }

    /// <summary>Where the name is drawn, kept clear of its neighbours.</summary>
    public double LabelY { get; init; }

    /// <summary>Which end of the name is pinned to that point.</summary>
    public string LabelAnchor { get; init; } = "middle";
}

/// <summary>An application modelling a class.</summary>
public sealed class MapLink
{
    /// <summary>Anchor slug of the application.</summary>
    public required string Slug { get; init; }

    /// <summary>The class name.</summary>
    public required string ClassName { get; init; }
}

/// <summary>The DevExpress versions the corpus sits on.</summary>
public sealed class VersionSpread
{
    /// <summary>The distinct declared versions, oldest first.</summary>
    public IReadOnlyList<string> Stops { get; init; } = [];

    /// <summary>Each application that declares one.</summary>
    public IReadOnlyList<VersionMark> Applications { get; init; } = [];

    /// <summary>The catalog every framework claim was checked against, when there is one.</summary>
    public string? CatalogVersion { get; init; }

    /// <summary>Applications whose declared version could not be read.</summary>
    public int Undeclared { get; init; }

    /// <summary>True when there is no spread to draw.</summary>
    public bool IsEmpty => Stops.Count == 0;

    /// <summary>True when the corpus sits on more than one DevExpress release.</summary>
    public bool IsSplit => Stops.Count > 1;
}

/// <summary>One application on the version strip.</summary>
public sealed class VersionMark
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Anchor slug.</summary>
    public required string Slug { get; init; }

    /// <summary>The declared version.</summary>
    public required string Version { get; init; }

    /// <summary>Index of its stop along the strip.</summary>
    public int Stop { get; init; }
}
