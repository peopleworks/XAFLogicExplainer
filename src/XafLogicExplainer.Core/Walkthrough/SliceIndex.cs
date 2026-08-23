using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Walkthrough;

/// <summary>One node and the extraction it was built from.</summary>
internal sealed record SliceItem(SliceNode Node, object? Payload)
{
    /// <summary>The same item, placed at its distance from the seed.</summary>
    internal SliceItem AtDistance(int distance) => this with { Node = Node with { Distance = distance } };
}

/// <summary>An action, with the controller that declares it.</summary>
internal sealed record ActionRef(ExtractedController Controller, ExtractedAction Action);

/// <summary>A method, with the controller that declares it.</summary>
internal sealed record MethodRef(ExtractedController Controller, ExtractedMethod Method);

/// <summary>A controller.</summary>
internal sealed record ControllerRef(ExtractedController Controller);

/// <summary>An entity.</summary>
internal sealed record EntityRef(ExtractedEntity Entity);

/// <summary>
/// Everything a walk needs to look up by name, built once per slice.
/// </summary>
/// <remarks>
/// Nodes are cached by id, so the same method reached twice is the same node rather than two that
/// happen to agree — which is what keeps the edge set an actual graph.
/// </remarks>
internal sealed class SliceIndex
{
    private readonly ExtractedProject _project;
    private readonly Dictionary<string, ExtractedEntity> _entities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<SliceItem>> _methodsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SliceItem> _cache = new(StringComparer.Ordinal);

    internal SliceIndex(ExtractedProject project)
    {
        _project = project;

        foreach (var entity in project.Entities)
            _entities.TryAdd(entity.ClassName, entity);

        foreach (var controller in project.Controllers)
        {
            foreach (var method in controller.Methods)
            {
                if (!_methodsByName.TryGetValue(method.Name, out var list))
                    _methodsByName[method.Name] = list = [];

                list.Add(Method(controller, method));
            }
        }
    }

    /// <summary>Finds what the caller named, trying the most specific kind first.</summary>
    /// <remarks>
    /// Actions before methods before controllers before entities: a seed is usually the thing a user
    /// can press, and the more specific the match the smaller and more useful the slice.
    /// </remarks>
    internal SliceItem? Resolve(string seed)
    {
        seed = seed.Trim();

        foreach (var controller in _project.Controllers)
        {
            foreach (var action in controller.Actions)
            {
                if (action.ActionId.Equals(seed, StringComparison.OrdinalIgnoreCase))
                    return Action(controller, action);
            }
        }

        // Both `CalculateCommissions` and `PayrollController.CalculateCommissions`, because a name
        // shared by two controllers can only be told apart by the second form.
        foreach (var controller in _project.Controllers)
        {
            foreach (var method in controller.Methods)
            {
                if (method.Name.Equals(seed, StringComparison.OrdinalIgnoreCase)
                    || $"{controller.ClassName}.{method.Name}".Equals(seed, StringComparison.OrdinalIgnoreCase))
                {
                    return Method(controller, method);
                }
            }
        }

        var namedController = _project.Controllers.FirstOrDefault(c =>
            c.ClassName.Equals(seed, StringComparison.OrdinalIgnoreCase));

        if (namedController is not null)
            return Controller(namedController);

        return _entities.TryGetValue(seed, out var entity) ? Entity(entity.ClassName) : null;
    }

    /// <summary>What to say when the seed matched nothing, including what might have been meant.</summary>
    internal string NothingMatched(string seed)
    {
        var everything = _project.Controllers.SelectMany(c => c.Actions).Select(a => a.ActionId)
            .Concat(_project.Controllers.SelectMany(c => c.Methods).Select(m => m.Name))
            .Concat(_project.Controllers.Select(c => c.ClassName))
            .Concat(_project.Entities.Select(e => e.ClassName))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var near = everything
            .Where(name => name.Contains(seed, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        var message = $"Nothing in {_project.ProjectName} is called '{seed}'. "
                      + "A walkthrough starts from an action, a controller method, a controller or an entity.";

        return near.Count == 0 ? message : $"{message}\n\nClosest names: {string.Join(", ", near)}.";
    }

    internal SliceItem Controller(ExtractedController controller) =>
        Cached($"controller:{controller.ClassName}", () => new SliceItem(
            new SliceNode
            {
                Id = $"controller:{controller.ClassName}",
                Kind = SliceNodeKind.Controller,
                Name = controller.ClassName,
                FilePath = controller.FilePath,
                Line = controller.Line,
            },
            new ControllerRef(controller)));

    internal SliceItem Action(ExtractedController controller, ExtractedAction action) =>
        Cached($"action:{controller.ClassName}.{action.ActionId}", () => new SliceItem(
            new SliceNode
            {
                Id = $"action:{controller.ClassName}.{action.ActionId}",
                Kind = SliceNodeKind.Action,
                Name = action.ActionId,
                Owner = controller.ClassName,
                // An action declared only in a constructor still has a place; one extracted before
                // locations existed has none, and falls back to its controller's file.
                FilePath = action.FilePath.Length > 0 ? action.FilePath : controller.FilePath,
                Line = action.Line,
            },
            new ActionRef(controller, action)));

    internal SliceItem Method(ExtractedController controller, ExtractedMethod method) =>
        Cached($"method:{controller.ClassName}.{method.Name}", () => new SliceItem(
            new SliceNode
            {
                Id = $"method:{controller.ClassName}.{method.Name}",
                Kind = SliceNodeKind.Method,
                Name = $"{controller.ClassName}.{method.Name}",
                Owner = controller.ClassName,
                FilePath = method.FilePath.Length > 0 ? method.FilePath : controller.FilePath,
                Line = method.Line,
            },
            new MethodRef(controller, method)));

    /// <summary>One controller's own method by name, which is what an unqualified call means.</summary>
    internal SliceItem? Method(ExtractedController controller, string name)
    {
        var matches = controller.Methods
            .Where(method => method.Name.Equals(name, StringComparison.Ordinal))
            .ToList();

        return matches.Count == 1 ? Method(controller, matches[0]) : null;
    }

    /// <summary>Every method in the project carrying one name.</summary>
    internal IReadOnlyList<SliceItem> MethodsNamed(string name) =>
        _methodsByName.TryGetValue(name, out var list) ? list : [];

    /// <summary>One entity by class name, or nothing when the name is not an entity.</summary>
    internal SliceItem? Entity(string name) =>
        _entities.TryGetValue(name, out var entity)
            ? Cached($"entity:{entity.ClassName}", () => new SliceItem(
                new SliceNode
                {
                    Id = $"entity:{entity.ClassName}",
                    Kind = SliceNodeKind.Entity,
                    Name = entity.ClassName,
                    FilePath = entity.FilePath,
                    Line = entity.Line,
                },
                new EntityRef(entity)))
            : null;

    /// <summary>
    /// The entities a property of this name navigates to, according to the declared model.
    /// </summary>
    /// <remarks>
    /// What makes <c>order.Customer</c> a touch of <c>Customer</c> a fact rather than a coincidence
    /// of naming. Several entities can declare a property of one name, and each is a real
    /// possibility, so all of them are returned rather than one being chosen arbitrarily.
    /// </remarks>
    internal IEnumerable<SliceItem> EntitiesNavigatedBy(string propertyName)
    {
        foreach (var related in _project.Entities
                     .SelectMany(entity => entity.Relationships)
                     .Where(relationship => relationship.PropertyName.Equals(propertyName, StringComparison.Ordinal))
                     .Select(relationship => relationship.RelatedEntity)
                     .Distinct(StringComparer.Ordinal))
        {
            if (Entity(related) is { } entity)
                yield return entity;
        }
    }

    /// <summary>The entities a controller is activated for.</summary>
    internal IEnumerable<SliceItem> EntitiesOf(ExtractedController controller)
    {
        if (controller.TargetObjectType is { Length: > 0 } target && Entity(target) is { } targeted)
            yield return targeted;

        foreach (var name in controller.ReferencedEntities)
        {
            if (Entity(name) is { } referenced)
                yield return referenced;
        }
    }

    /// <summary>Every rule that governs an entity, including the ones it inherits.</summary>
    /// <remarks>
    /// Cited at the class that <em>declares</em> the rule rather than at the one carrying it, which
    /// is the same choice the rest of the project makes. The attribute's own line is not extracted,
    /// so the citation reaches the class and stops there.
    /// </remarks>
    internal IEnumerable<SliceItem> RulesOf(ExtractedEntity entity)
    {
        foreach (var rule in entity.ValidationRules)
        {
            var key = rule.Id is { Length: > 0 } id ? id : $"{rule.RuleType}:{rule.TargetProperty ?? "*"}";
            var declarer = Declarer(rule.InheritedFrom, entity);

            yield return Cached($"rule:{entity.ClassName}.{key}", () => new SliceItem(
                new SliceNode
                {
                    Id = $"rule:{entity.ClassName}.{key}",
                    Kind = SliceNodeKind.ValidationRule,
                    Name = rule.Id is { Length: > 0 } named
                        ? named
                        : $"{rule.RuleType} on {rule.TargetProperty ?? entity.ClassName}",
                    Owner = declarer.ClassName,
                    FilePath = declarer.FilePath,
                    Line = declarer.Line,
                },
                null));
        }

        foreach (var rule in entity.AppearanceRules)
        {
            var key = rule.Id is { Length: > 0 } id ? id : $"appearance:{rule.TargetItems ?? "*"}";
            var declarer = Declarer(rule.InheritedFrom, entity);

            yield return Cached($"appearance:{entity.ClassName}.{key}", () => new SliceItem(
                new SliceNode
                {
                    Id = $"appearance:{entity.ClassName}.{key}",
                    Kind = SliceNodeKind.AppearanceRule,
                    // An unnamed rule is ordinary rather than an omission: one written on a property
                    // already says what it governs.
                    Name = rule.Id is { Length: > 0 } named
                        ? named
                        : $"appearance on {rule.TargetItems ?? entity.ClassName}",
                    Owner = declarer.ClassName,
                    FilePath = declarer.FilePath,
                    Line = declarer.Line,
                },
                null));
        }
    }

    private ExtractedEntity Declarer(string? inheritedFrom, ExtractedEntity carrier) =>
        inheritedFrom is { Length: > 0 } from && _entities.TryGetValue(from, out var declared)
            ? declared
            : carrier;

    private SliceItem Cached(string id, Func<SliceItem> build)
    {
        if (!_cache.TryGetValue(id, out var item))
            _cache[id] = item = build();

        return item;
    }
}
