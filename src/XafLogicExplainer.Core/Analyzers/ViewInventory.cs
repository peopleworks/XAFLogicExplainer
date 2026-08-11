using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Enumerates every screen the application has.
/// </summary>
/// <remarks>
/// The Model Editor is the wrong place to look, and it is the obvious one. A <c>.xafml</c> file
/// stores <em>differences</em>, so it holds only the views somebody changed; the rest are generated
/// by XAF at startup and exist in no file at all. The generators are public and their id rules are
/// fixed, so the complete list follows from the business classes:
/// <list type="bullet">
///   <item><c>{Class}_ListView</c>, <c>{Class}_DetailView</c> and <c>{Class}_LookupListView</c> for
///   every class in the model.</item>
///   <item><c>{Class}_{Collection}_ListView</c> for every collection property.</item>
/// </list>
/// The Model Editor then contributes captions, and any view that exists only there.
/// </remarks>
public static class ViewInventory
{
    /// <summary>
    /// Builds the view inventory for an extracted application.
    /// </summary>
    /// <param name="project">The extracted application.</param>
    public static List<ExtractedView> Build(ExtractedProject project)
    {
        var views = new Dictionary<string, ExtractedView>(StringComparer.Ordinal);

        foreach (var entity in project.Entities.Where(e => e.IsPersistent))
            AddGenerated(views, entity);

        MergeModelViews(views, project.ModelEditorInfo);
        MarkNavigation(views, project);

        return [.. views.Values.OrderBy(view => view.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Adds the four kinds of view XAF generates for one business class.
    /// </summary>
    private static void AddGenerated(Dictionary<string, ExtractedView> views, ExtractedEntity entity)
    {
        Add(views, new ExtractedView
        {
            Id = $"{entity.ClassName}_ListView",
            ViewType = ModelViewType.ListView,
            ObjectType = entity.ClassName,
            // Reached from navigation or an action. A collection shown inside a detail view gets
            // its own generated id instead, which is why this one is not "either".
            Nesting = ViewNesting.Root,
            Origin = ViewOrigin.Generated,
        });

        Add(views, new ExtractedView
        {
            Id = $"{entity.ClassName}_DetailView",
            ViewType = ModelViewType.DetailView,
            ObjectType = entity.ClassName,
            // XAF generates no separate nested detail view: the same id is used when a record is
            // opened from navigation and when a DetailPropertyEditor shows it inside another view.
            Nesting = ViewNesting.Either,
            Origin = ViewOrigin.Generated,
        });

        Add(views, new ExtractedView
        {
            Id = $"{entity.ClassName}_LookupListView",
            ViewType = ModelViewType.ListView,
            ObjectType = entity.ClassName,
            Nesting = ViewNesting.Nested,
            Origin = ViewOrigin.Generated,
        });

        foreach (var property in entity.Properties.Where(p => p.IsCollection))
        {
            var itemType = entity.Relationships
                .FirstOrDefault(r => r.PropertyName == property.Name)?.RelatedEntity;

            Add(views, new ExtractedView
            {
                Id = $"{entity.ClassName}_{property.Name}_ListView",
                ViewType = ModelViewType.ListView,
                ObjectType = itemType ?? ItemTypeOf(property.TypeName),
                Nesting = ViewNesting.Nested,
                Origin = ViewOrigin.Generated,
                OwnerEntity = entity.ClassName,
                OwnerProperty = property.Name,
            });
        }
    }

    /// <summary>
    /// Folds in what the Model Editor says: captions on generated views, and views that exist only
    /// there because somebody cloned or hand-wrote them.
    /// </summary>
    private static void MergeModelViews(Dictionary<string, ExtractedView> views, ExtractedModelInfo? model)
    {
        if (model is null)
            return;

        foreach (var modelView in model.Views)
        {
            if (views.TryGetValue(modelView.Id, out var existing))
            {
                existing.Origin = ViewOrigin.Customized;
                existing.Caption ??= modelView.Caption;
                continue;
            }

            views[modelView.Id] = new ExtractedView
            {
                Id = modelView.Id,
                ViewType = modelView.ViewType,
                // A cloned view keeps the class name as its prefix, which is the only clue to what
                // it shows without a compilation. Left null when nothing matches, rather than
                // guessed.
                ObjectType = null,
                Nesting = ViewNesting.Either,
                Origin = ViewOrigin.ModelOnly,
                Caption = modelView.Caption,
            };
        }
    }

    /// <summary>
    /// Marks the views a navigation item opens.
    /// </summary>
    private static void MarkNavigation(Dictionary<string, ExtractedView> views, ExtractedProject project)
    {
        foreach (var item in project.ModelEditorInfo?.NavigationItems?.Groups.SelectMany(g => g.Items) ?? [])
        {
            if (item.ViewId is { Length: > 0 } viewId && views.TryGetValue(viewId, out var view))
                view.InNavigation = true;
        }

        // DefaultClassOptions puts a class in navigation without anything being written down.
        foreach (var entity in project.Entities.Where(e => e.IsDefaultClassOptions))
        {
            if (views.TryGetValue($"{entity.ClassName}_ListView", out var view))
                view.InNavigation = true;
        }
    }

    /// <summary>
    /// Resolves the class a cloned model view most likely shows, from its id prefix.
    /// </summary>
    /// <remarks>
    /// Applied only to views the Model Editor introduced, and only when the prefix is the name of a
    /// class this application actually has — so a wrong guess would need a class named after the
    /// prefix of an unrelated view.
    /// </remarks>
    /// <param name="views">The inventory, updated in place.</param>
    /// <param name="entities">Known business classes.</param>
    public static void ResolveModelOnlyObjectTypes(
        IReadOnlyList<ExtractedView> views,
        IReadOnlyList<ExtractedEntity> entities)
    {
        var byName = entities.Select(e => e.ClassName).ToHashSet(StringComparer.Ordinal);

        foreach (var view in views.Where(v => v.Origin == ViewOrigin.ModelOnly && v.ObjectType is null))
        {
            var underscore = view.Id.IndexOf('_');

            if (underscore <= 0)
                continue;

            var prefix = view.Id[..underscore];

            if (byName.Contains(prefix))
                view.ObjectType = prefix;
        }
    }

    /// <summary>
    /// Reads the item type out of a collection type name.
    /// </summary>
    private static string? ItemTypeOf(string typeName)
    {
        var open = typeName.IndexOf('<');
        var close = typeName.LastIndexOf('>');

        if (open <= 0 || close <= open)
            return null;

        var argument = typeName[(open + 1)..close].Trim();
        var lastDot = argument.LastIndexOf('.');

        return lastDot >= 0 ? argument[(lastDot + 1)..] : argument;
    }

    private static void Add(Dictionary<string, ExtractedView> views, ExtractedView view) =>
        views.TryAdd(view.Id, view);
}
