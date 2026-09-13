using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Finds the business class a name written in source means, and names every class so that no two
/// read alike.
/// </summary>
/// <remarks>
/// A class name is not an identity. XAF accepts two business classes of one name in two namespaces
/// once one of them is given its own view id prefix, and every place that looked a class up by its
/// bare name then either threw or picked one of the two without saying so (#84).
/// <para>
/// Syntax only, like the rest of the extraction, so a name is looked up the way C# looks it up as far
/// as the source shows it: a qualified name by the namespace written in front of it, a bare name in
/// the namespace it is written in and then in each namespace enclosing that one. Using directives are
/// not followed, so a bare name that still matches two classes resolves to neither — a guess here
/// would draw an arrow to the wrong table with nothing on the page to show it was a guess.
/// </para>
/// </remarks>
public sealed class EntityDirectory
{
    private readonly Dictionary<string, List<ExtractedEntity>> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<ExtractedEntity, string> _labels = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Indexes one application's business classes.
    /// </summary>
    /// <param name="entities">The classes, as extracted.</param>
    public EntityDirectory(IEnumerable<ExtractedEntity> entities)
    {
        foreach (var entity in entities)
        {
            if (!_byName.TryGetValue(entity.ClassName, out var sameName))
                _byName[entity.ClassName] = sameName = [];

            sameName.Add(entity);
        }

        foreach (var sameName in _byName.Values.Where(group => group.Count > 1))
            LabelApart(sameName);
    }

    /// <summary>
    /// Whether more than one class of the application has this name.
    /// </summary>
    /// <param name="className">A bare class name.</param>
    public bool IsShared(string className) =>
        _byName.TryGetValue(className, out var sameName) && sameName.Count > 1;

    /// <summary>
    /// The class a type name written in source means, or null when it is not one of the
    /// application's classes or cannot be told apart from another.
    /// </summary>
    /// <param name="written">The type as written: <c>Tag</c>, <c>Catalog.Tag</c>, <c>global::A.B.Tag</c>.</param>
    /// <param name="fromNamespace">The namespace the name is written in, when known.</param>
    public ExtractedEntity? Resolve(string? written, string? fromNamespace = null)
    {
        if (string.IsNullOrWhiteSpace(written))
            return null;

        var name = written.Trim().TrimEnd('?');

        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name["global::".Length..];

        var dot = name.LastIndexOf('.');
        var className = dot < 0 ? name : name[(dot + 1)..];

        if (!_byName.TryGetValue(className, out var sameName))
            return null;

        if (dot < 0)
        {
            return sameName.Count == 1
                ? sameName[0]
                : Innermost(sameName, fromNamespace, entity => entity.Namespace);
        }

        var qualifier = name[..dot];

        // A qualified name can be relative to any namespace enclosing the place it is written in,
        // so Catalog.Tag written in A.B means A.B.Catalog.Tag before it means Catalog.Tag.
        var inScope = Innermost(sameName, fromNamespace, entity => Relative(entity.Namespace, qualifier));
        if (inScope is not null)
            return inScope;

        var bySuffix = sameName
            .Where(entity => entity.Namespace == qualifier
                             || entity.Namespace.EndsWith("." + qualifier, StringComparison.Ordinal))
            .ToList();

        return bySuffix.Count == 1 ? bySuffix[0] : null;
    }

    /// <summary>
    /// The name to print for a class: its bare name, or as much of its namespace as tells it apart
    /// from the other classes of that name.
    /// </summary>
    /// <param name="entity">A class of this application.</param>
    public string Label(ExtractedEntity entity) =>
        _labels.TryGetValue(entity, out var label) ? label : entity.ClassName;

    /// <summary>
    /// The name to print for a type written in source: the label of the class it resolves to when
    /// that class shares its name, and otherwise the text as written.
    /// </summary>
    /// <param name="written">The type as written.</param>
    /// <param name="fromNamespace">The namespace it is written in, when known.</param>
    public string LabelOf(string written, string? fromNamespace = null) =>
        Resolve(written, fromNamespace) is { } entity && IsShared(entity.ClassName)
            ? Label(entity)
            : written;

    /// <summary>
    /// A fragment identifier for a class: the bare name, which every page has always used, and the
    /// full name only for a class that shares its name.
    /// </summary>
    /// <param name="entity">A class of this application.</param>
    public string Anchor(ExtractedEntity entity) =>
        IsShared(entity.ClassName) && entity.Namespace.Length > 0
            ? $"{entity.Namespace}.{entity.ClassName}"
            : entity.ClassName;

    /// <summary>
    /// The one class whose namespace, seen through <paramref name="namespaceOf"/>, is the innermost
    /// namespace enclosing <paramref name="fromNamespace"/>.
    /// </summary>
    private static ExtractedEntity? Innermost(
        List<ExtractedEntity> sameName,
        string? fromNamespace,
        Func<ExtractedEntity, string?> namespaceOf)
    {
        for (var scope = fromNamespace; !string.IsNullOrEmpty(scope); scope = Parent(scope))
        {
            var here = sameName.Where(entity => namespaceOf(entity) == scope).ToList();

            if (here.Count > 0)
                return here.Count == 1 ? here[0] : null;
        }

        return null;
    }

    /// <summary>
    /// The namespace a qualified name would have to be written in for <paramref name="qualifier"/>
    /// to reach <paramref name="entityNamespace"/>, or null when it cannot.
    /// </summary>
    private static string? Relative(string entityNamespace, string qualifier) =>
        entityNamespace.EndsWith("." + qualifier, StringComparison.Ordinal)
            ? entityNamespace[..^(qualifier.Length + 1)]
            : null;

    private static string? Parent(string ns)
    {
        var dot = ns.LastIndexOf('.');
        return dot < 0 ? null : ns[..dot];
    }

    /// <summary>
    /// Gives each class of one name the shortest namespace tail that no other class of that name has.
    /// </summary>
    private void LabelApart(List<ExtractedEntity> sameName)
    {
        var segments = sameName.ToDictionary<ExtractedEntity, ExtractedEntity, string[]>(
            entity => entity,
            entity => entity.Namespace.Split('.', StringSplitOptions.RemoveEmptyEntries),
            ReferenceEqualityComparer.Instance);

        var deepest = segments.Values.Max(parts => parts.Length);

        for (var length = 1; length <= deepest; length++)
        {
            var tails = sameName.ToDictionary<ExtractedEntity, ExtractedEntity, string>(
                entity => entity,
                entity => string.Join('.', segments[entity].TakeLast(length)),
                ReferenceEqualityComparer.Instance);

            if (tails.Values.Distinct(StringComparer.Ordinal).Count() < sameName.Count && length < deepest)
                continue;

            foreach (var (entity, tail) in tails)
                _labels[entity] = tail.Length == 0 ? entity.ClassName : $"{tail}.{entity.ClassName}";

            return;
        }
    }
}
