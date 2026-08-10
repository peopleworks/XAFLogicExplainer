using System.Xml.Linq;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Parses xafml files and merges Model Editor customizations into a unified model snapshot.
/// </summary>
public class ModelAnalyzer
{
    /// <summary>
    /// Analyzes available xafml files and returns merged model metadata.
    /// </summary>
    /// <param name="sourceDirectory">Project source root.</param>
    /// <param name="options">Extraction options controlling model discovery.</param>
    /// <returns>Merged model metadata or null when model extraction is disabled or no files are found.</returns>
    public ExtractedModelInfo? AnalyzeModel(string sourceDirectory, ExtractionOptions options)
    {
        if (!options.IncludeModelEditor) return null;

        var xafmlFiles = FindXafmlFiles(sourceDirectory, options);
        if (xafmlFiles.Count == 0) return null;

        var result = new ExtractedModelInfo();

        foreach (var file in xafmlFiles)
        {
            try
            {
                var doc = XDocument.Load(file);
                var root = doc.Root;
                if (root == null) continue;

                result.SourceFiles.Add(file);
                MergeFromDocument(result, root);
            }
            catch
            {
                // Skip malformed XML files
            }
        }

        return result.SourceFiles.Count > 0 ? result : null;
    }

    /// <summary>
    /// Finds model files in the current module and optionally sibling platform modules.
    /// </summary>
    private static List<string> FindXafmlFiles(string sourceDirectory, ExtractionOptions options)
    {
        var files = new List<string>();

        // 1. Module-level: Model.DesignedDiffs.xafml
        var moduleXafml = Path.Combine(sourceDirectory, "Model.DesignedDiffs.xafml");
        if (File.Exists(moduleXafml))
            files.Add(moduleXafml);

        // Also check for Model.xafml in the source directory itself
        var moduleModel = Path.Combine(sourceDirectory, "Model.xafml");
        if (File.Exists(moduleModel))
            files.Add(moduleModel);

        // 2. Discover sibling platform projects (Blazor.Server, Win, etc.)
        if (options.DiscoverPlatformModels)
        {
            var parentDir = Directory.GetParent(sourceDirectory)?.FullName;
            if (parentDir != null)
            {
                foreach (var siblingDir in Directory.GetDirectories(parentDir))
                {
                    if (siblingDir.Equals(sourceDirectory, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Check for Model.DesignedDiffs.xafml in sibling
                    var siblingDiffs = Path.Combine(siblingDir, "Model.DesignedDiffs.xafml");
                    if (File.Exists(siblingDiffs))
                        files.Add(siblingDiffs);

                    // Check for Model.xafml in sibling
                    var siblingModel = Path.Combine(siblingDir, "Model.xafml");
                    if (File.Exists(siblingModel))
                        files.Add(siblingModel);
                }
            }
        }

        return files;
    }

    /// <summary>
    /// Merges one xafml root node into the aggregate model.
    /// </summary>
    private static void MergeFromDocument(ExtractedModelInfo result, XElement root)
    {
        result.ApplicationTitle ??= root.Attribute("Title")?.Value;

        ParseBOModel(result, root);
        ParseViews(result, root);
        ParseNavigationItems(result, root);
        ParseOptions(result, root);
        ParseSchemaModules(result, root);
    }

    /// <summary>
    /// Parses BOModel class customizations.
    /// </summary>
    private static void ParseBOModel(ExtractedModelInfo result, XElement root)
    {
        var boModel = root.Element("BOModel");
        if (boModel == null) return;

        foreach (var classEl in boModel.Elements("Class"))
        {
            var name = classEl.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            var existing = result.BOModelClasses.FirstOrDefault(c => c.FullName == name);
            if (existing != null)
            {
                // Overlay: later values overwrite
                existing.Caption = classEl.Attribute("Caption")?.Value ?? existing.Caption;
                if (bool.TryParse(classEl.Attribute("IsCloneable")?.Value, out var cloneable) && cloneable)
                    existing.IsCloneable = true;
                MergeCustomAttributes(existing.CustomAttributes, classEl, ["Name", "Caption", "IsCloneable", "IsNewNode"]);
            }
            else
            {
                var info = new ModelClassInfo
                {
                    FullName = name,
                    Caption = classEl.Attribute("Caption")?.Value,
                };
                if (bool.TryParse(classEl.Attribute("IsCloneable")?.Value, out var cloneable2))
                    info.IsCloneable = cloneable2;
                MergeCustomAttributes(info.CustomAttributes, classEl, ["Name", "Caption", "IsCloneable", "IsNewNode"]);
                result.BOModelClasses.Add(info);
            }
        }
    }

    /// <summary>
    /// Parses view metadata and merges by view identifier.
    /// </summary>
    private static void ParseViews(ExtractedModelInfo result, XElement root)
    {
        var views = root.Element("Views");
        if (views == null) return;

        foreach (var viewEl in views.Elements())
        {
            var viewType = viewEl.Name.LocalName switch
            {
                "ListView" => ModelViewType.ListView,
                "DetailView" => ModelViewType.DetailView,
                "DashboardView" => ModelViewType.DashboardView,
                _ => (ModelViewType?)null
            };
            if (viewType == null) continue;

            var id = viewEl.Attribute("Id")?.Value;
            if (string.IsNullOrEmpty(id)) continue;

            var existing = result.Views.FirstOrDefault(v => v.Id == id);
            if (existing != null)
            {
                // Overlay
                OverlayViewAttributes(existing, viewEl);
            }
            else
            {
                var view = new ModelViewInfo
                {
                    Id = id,
                    ViewType = viewType.Value,
                };
                OverlayViewAttributes(view, viewEl);
                result.Views.Add(view);
            }
        }
    }

    /// <summary>
    /// Overlays view attributes, columns, and filters onto an existing view descriptor.
    /// </summary>
    private static void OverlayViewAttributes(ModelViewInfo view, XElement viewEl)
    {
        view.Caption = viewEl.Attribute("Caption")?.Value ?? view.Caption;
        view.Criteria = viewEl.Attribute("Criteria")?.Value ?? view.Criteria;
        view.EditorTypeName = viewEl.Attribute("EditorTypeName")?.Value ?? view.EditorTypeName;

        if (bool.TryParse(viewEl.Attribute("AllowEdit")?.Value, out var ae))
            view.AllowEdit = ae;
        if (bool.TryParse(viewEl.Attribute("AllowDelete")?.Value, out var ad))
            view.AllowDelete = ad;
        if (bool.TryParse(viewEl.Attribute("AllowNew")?.Value, out var an))
            view.AllowNew = an;

        // Columns
        var columnsEl = viewEl.Element("Columns");
        if (columnsEl != null)
        {
            foreach (var colEl in columnsEl.Elements("ColumnInfo"))
            {
                var colId = colEl.Attribute("Id")?.Value;
                if (string.IsNullOrEmpty(colId)) continue;

                var existingCol = view.Columns.FirstOrDefault(c => c.Id == colId);
                if (existingCol != null)
                {
                    existingCol.PropertyName = colEl.Attribute("PropertyName")?.Value ?? existingCol.PropertyName;
                    existingCol.Caption = colEl.Attribute("Caption")?.Value ?? existingCol.Caption;
                    existingCol.SortOrder = colEl.Attribute("SortOrder")?.Value ?? existingCol.SortOrder;
                    if (int.TryParse(colEl.Attribute("Index")?.Value, out var idx))
                        existingCol.Index = idx;
                    if (int.TryParse(colEl.Attribute("Width")?.Value, out var w))
                        existingCol.Width = w;
                }
                else
                {
                    var col = new ModelColumnInfo { Id = colId };
                    col.PropertyName = colEl.Attribute("PropertyName")?.Value;
                    col.Caption = colEl.Attribute("Caption")?.Value;
                    col.SortOrder = colEl.Attribute("SortOrder")?.Value;
                    if (int.TryParse(colEl.Attribute("Index")?.Value, out var idx))
                        col.Index = idx;
                    if (int.TryParse(colEl.Attribute("Width")?.Value, out var w))
                        col.Width = w;
                    view.Columns.Add(col);
                }
            }
        }

        // Filters
        var filtersEl = viewEl.Element("Filters");
        if (filtersEl != null)
        {
            view.CurrentFilterId = filtersEl.Attribute("CurrentFilterId")?.Value ?? view.CurrentFilterId;

            foreach (var filterEl in filtersEl.Elements("Filter"))
            {
                var filterId = filterEl.Attribute("Id")?.Value;
                if (string.IsNullOrEmpty(filterId)) continue;

                var existingFilter = view.Filters.FirstOrDefault(f => f.Id == filterId);
                if (existingFilter != null)
                {
                    existingFilter.Caption = filterEl.Attribute("Caption")?.Value ?? existingFilter.Caption;
                    existingFilter.Criteria = filterEl.Attribute("Criteria")?.Value ?? existingFilter.Criteria;
                }
                else
                {
                    view.Filters.Add(new ModelFilterInfo
                    {
                        Id = filterId,
                        Caption = filterEl.Attribute("Caption")?.Value,
                        Criteria = filterEl.Attribute("Criteria")?.Value,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Parses navigation metadata from model nodes.
    /// </summary>
    private static void ParseNavigationItems(ExtractedModelInfo result, XElement root)
    {
        var navEl = root.Element("NavigationItems");
        if (navEl == null) return;

        result.NavigationItems ??= new ModelNavigationInfo();
        var nav = result.NavigationItems;

        nav.NavigationStyle = navEl.Attribute("NavigationStyle")?.Value ?? nav.NavigationStyle;
        nav.StartupNavigationItem = navEl.Attribute("StartupNavigationItem")?.Value ?? nav.StartupNavigationItem;

        var itemsEl = navEl.Element("Items");
        if (itemsEl == null) return;

        foreach (var groupEl in itemsEl.Elements("Item"))
        {
            var groupId = groupEl.Attribute("Id")?.Value;
            if (string.IsNullOrEmpty(groupId)) continue;

            var existingGroup = nav.Groups.FirstOrDefault(g => g.Id == groupId);
            if (existingGroup == null)
            {
                existingGroup = new ModelNavigationGroup { Id = groupId };
                nav.Groups.Add(existingGroup);
            }

            existingGroup.Caption = groupEl.Attribute("Caption")?.Value ?? existingGroup.Caption;
            existingGroup.ImageName = groupEl.Attribute("ImageName")?.Value ?? existingGroup.ImageName;
            if (int.TryParse(groupEl.Attribute("Index")?.Value, out var gi))
                existingGroup.Index = gi;

            // Child items
            var childItemsEl = groupEl.Element("Items");
            if (childItemsEl == null) continue;

            foreach (var itemEl in childItemsEl.Elements("Item"))
            {
                var itemId = itemEl.Attribute("Id")?.Value;
                if (string.IsNullOrEmpty(itemId)) continue;

                var existingItem = existingGroup.Items.FirstOrDefault(i => i.Id == itemId);
                if (existingItem == null)
                {
                    existingItem = new ModelNavigationItemEntry { Id = itemId };
                    existingGroup.Items.Add(existingItem);
                }

                existingItem.Caption = itemEl.Attribute("Caption")?.Value ?? existingItem.Caption;
                existingItem.ViewId = itemEl.Attribute("ViewId")?.Value ?? existingItem.ViewId;
                existingItem.ImageName = itemEl.Attribute("ImageName")?.Value ?? existingItem.ImageName;
                if (int.TryParse(itemEl.Attribute("Index")?.Value, out var ii))
                    existingItem.Index = ii;
            }
        }
    }

    /// <summary>
    /// Parses application options metadata from model nodes.
    /// </summary>
    private static void ParseOptions(ExtractedModelInfo result, XElement root)
    {
        var optionsEl = root.Element("Options");
        if (optionsEl == null) return;

        result.Options ??= new ModelOptionsInfo();
        var opts = result.Options;

        opts.UIType = optionsEl.Attribute("UIType")?.Value ?? opts.UIType;
        opts.FormStyle = optionsEl.Attribute("FormStyle")?.Value ?? opts.FormStyle;
        opts.Skin = optionsEl.Attribute("Skin")?.Value ?? opts.Skin;

        var layoutOpts = optionsEl.Element("LayoutManagerOptions");
        if (layoutOpts != null)
            opts.RequiredFieldMark = layoutOpts.Attribute("RequiredFieldMark")?.Value ?? opts.RequiredFieldMark;
    }

    /// <summary>
    /// Parses registered schema modules.
    /// </summary>
    private static void ParseSchemaModules(ExtractedModelInfo result, XElement root)
    {
        var schemaEl = root.Element("SchemaModules");
        if (schemaEl == null) return;

        foreach (var modEl in schemaEl.Elements("SchemaModule"))
        {
            var name = modEl.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            if (result.SchemaModules.Any(s => s.Name == name))
                continue; // deduplicate

            result.SchemaModules.Add(new ModelSchemaModuleInfo
            {
                Name = name,
                Version = modEl.Attribute("Version")?.Value,
            });
        }
    }

    /// <summary>
    /// Copies non-core XML attributes into a custom dictionary.
    /// </summary>
    private static void MergeCustomAttributes(Dictionary<string, string> dict, XElement element, string[] excludeNames)
    {
        var excludeSet = new HashSet<string>(excludeNames, StringComparer.OrdinalIgnoreCase);
        foreach (var attr in element.Attributes())
        {
            if (!excludeSet.Contains(attr.Name.LocalName))
                dict[attr.Name.LocalName] = attr.Value;
        }
    }
}
