using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Mcp.Tools;

/// <summary>
/// Tools that return the full detail of one part of an application.
/// </summary>
/// <remarks>
/// Called after discovery has established what exists. Each returns everything known about a single
/// thing, including source code where that is what answers the question.
/// </remarks>
[McpServerToolType]
public sealed class XafDetailTools
{
    /// <summary>
    /// Longest method body reproduced in a response.
    /// </summary>
    /// <remarks>
    /// Long enough for a normal XAF action handler, short enough that one pathological method
    /// cannot fill an agent's context window.
    /// </remarks>
    private const int MaxCodeLength = 6000;

    private readonly XafProjectContext _context;

    /// <summary>Creates the tool set.</summary>
    public XafDetailTools(XafProjectContext context) => _context = context;

    /// <summary>
    /// Returns everything known about one entity.
    /// </summary>
    [McpServerTool(Name = "xaf_entity")]
    [Description(
        "Full detail of one business entity: every property with its type and attributes, " +
        "relationships to other entities, validation rules, appearance rules, and calculated " +
        "property expressions. Use before writing or changing any code that touches an entity.")]
    public async Task<string> EntityAsync(
        [Description("Entity class name, e.g. 'Invoice'. Case-insensitive.")] string name,
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        var entity = app.Entities.FirstOrDefault(e =>
            e.ClassName.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entity is null)
            return NotFound("entity", name, app.Entities.Select(e => e.ClassName));

        var sb = new StringBuilder();
        sb.AppendLine($"# {entity.ClassName}");
        sb.AppendLine();
        sb.AppendLine($"Namespace `{entity.Namespace}`, base class `{entity.BaseType}`.");

        if (At(entity.FilePath, entity.Line) is { Length: > 0 } declaredAt)
            sb.AppendLine($"Declared at {declaredAt}.");

        if (!string.IsNullOrWhiteSpace(entity.Description))
            sb.AppendLine($"\n{entity.Description}");

        if (!string.IsNullOrWhiteSpace(entity.ModelCaption))
            sb.AppendLine($"\nModel Editor caption: \"{entity.ModelCaption}\".");

        if (!string.IsNullOrWhiteSpace(entity.DefaultProperty))
            sb.AppendLine($"\nDisplayed by its `{entity.DefaultProperty}` property.");

        sb.AppendLine();
        sb.AppendLine("## Properties");
        sb.AppendLine();

        foreach (var property in entity.Properties)
        {
            var flags = new List<string>();
            if (property.IsKey) flags.Add("key");
            if (property.IsRequired) flags.Add("required");
            if (property.IsUnique) flags.Add("unique — the database refuses a duplicate");
            if (property.IsCollection) flags.Add("collection");
            if (property.Size is > 0) flags.Add($"max {property.Size}");
            if (property.ImmediatePostData) flags.Add("immediate post");

            sb.Append($"- **{property.Name}** `{property.TypeName}`");
            if (flags.Count > 0) sb.Append($" ({string.Join(", ", flags)})");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(property.PersistentAlias))
                sb.AppendLine($"  - calculated: `{property.PersistentAlias}`");
            if (!string.IsNullOrWhiteSpace(property.Description))
                sb.AppendLine($"  - {XafDiscoveryTools.Compact(property.Description)}");
            if (!string.IsNullOrWhiteSpace(property.DataSourceCriteria))
                sb.AppendLine($"  - lookup filtered by: `{property.DataSourceCriteria}`");
            if (!string.IsNullOrWhiteSpace(property.DefaultValue))
                sb.AppendLine($"  - default: `{property.DefaultValue}`");
        }

        if (entity.Relationships.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Relationships");
            sb.AppendLine();
            foreach (var relationship in entity.Relationships.OrderByDescending(r => r.InheritedFrom is null))
            {
                var owned = relationship.IsAggregated ? ", aggregated (owns the children)" : "";
                var association = string.IsNullOrWhiteSpace(relationship.AssociationName)
                    ? ""
                    : $", association \"{relationship.AssociationName}\"";
                var declarer = relationship.InheritedFrom is { Length: > 0 } from
                    ? $", inherited from `{from}`"
                    : "";
                sb.AppendLine($"- `{relationship.PropertyName}` → **{relationship.RelatedEntity}** ({relationship.Type}{association}{owned}{declarer})");
            }
        }

        AppendRules(sb, entity);

        return sb.ToString();
    }

    /// <summary>
    /// Returns one controller, including the code its actions run.
    /// </summary>
    [McpServerTool(Name = "xaf_controller")]
    [Description(
        "Full detail of one controller: which views it applies to, every action it defines with " +
        "the actual C# that runs when the action fires, and its helper methods. Use when asked " +
        "what a button or command actually does.")]
    public async Task<string> ControllerAsync(
        [Description("Controller class name, e.g. 'ApproveInvoiceController'. Case-insensitive.")] string name,
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        var controller = app.Controllers.FirstOrDefault(c =>
            c.ClassName.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (controller is null)
            return NotFound("controller", name, app.Controllers.Select(c => c.ClassName));

        var sb = new StringBuilder();
        sb.AppendLine($"# {controller.ClassName}");
        sb.AppendLine();
        sb.AppendLine($"Base class `{controller.BaseControllerType}`.");

        if (At(controller.FilePath, controller.Line) is { Length: > 0 } controllerAt)
            sb.AppendLine($"Declared at {controllerAt}.");
        sb.AppendLine($"Applies to: {controller.TargetObjectType ?? "any object type"}" +
                      $"{(string.IsNullOrWhiteSpace(controller.TargetViewType) ? "" : $", {controller.TargetViewType} only")}.");

        // Present only when a ground-truth catalog identified the base as a DevExpress type. It
        // changes the reading of everything below: this controller is modifying shipped behavior,
        // not adding something alongside it.
        if (!string.IsNullOrWhiteSpace(controller.FrameworkBaseType))
        {
            sb.AppendLine();
            sb.Append($"**Extends the built-in `{controller.FrameworkBaseType}`**");

            if (!string.IsNullOrWhiteSpace(controller.FrameworkBaseSummary))
                sb.Append($" — {controller.FrameworkBaseSummary}");

            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(controller.FrameworkBaseDocumentationUrl))
                sb.AppendLine($"Official documentation: {controller.FrameworkBaseDocumentationUrl}");
        }

        if (!string.IsNullOrWhiteSpace(controller.BusinessLogicSummary))
        {
            sb.AppendLine();
            sb.AppendLine("## What it does");
            sb.AppendLine();
            sb.AppendLine(controller.BusinessLogicSummary);
        }

        if (controller.Actions.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Defines no actions; it customizes behavior through view events or defaults.");
        }

        foreach (var action in controller.Actions)
        {
            sb.AppendLine();
            sb.AppendLine($"## Action `{action.ActionId}`");
            sb.AppendLine();
            sb.AppendLine($"- Type: {action.ActionType}");

            if (Within(controller.FilePath, action.FilePath, action.Line) is { Length: > 0 } actionAt)
                sb.AppendLine($"- Declared at {actionAt}");
            if (!string.IsNullOrWhiteSpace(action.Caption)) sb.AppendLine($"- Caption: \"{action.Caption}\"");
            if (!string.IsNullOrWhiteSpace(action.Category)) sb.AppendLine($"- Category: {action.Category}");
            if (!string.IsNullOrWhiteSpace(action.ConfirmationMessage)) sb.AppendLine($"- Confirms with: \"{action.ConfirmationMessage}\"");
            if (!string.IsNullOrWhiteSpace(action.EnabledCriteria)) sb.AppendLine($"- Enabled when: `{action.EnabledCriteria}`");

            if (!string.IsNullOrWhiteSpace(action.BusinessLogicSummary))
            {
                sb.AppendLine();
                sb.AppendLine(action.BusinessLogicSummary);
            }

            if (!string.IsNullOrWhiteSpace(action.ExecuteMethodBody))
            {
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(Cap(action.ExecuteMethodBody));
                sb.AppendLine("```");
            }
        }

        var helpers = controller.Methods
            .Where(m => !string.IsNullOrWhiteSpace(m.Body))
            .ToList();

        if (helpers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Helper methods");

            foreach (var method in helpers)
            {
                sb.AppendLine();
                sb.AppendLine($"### `{method.ReturnType} {method.Name}({string.Join(", ", method.Parameters)})`");
                sb.AppendLine();

                if (Within(controller.FilePath, method.FilePath, method.Line) is { Length: > 0 } methodAt)
                {
                    sb.AppendLine($"Declared at {methodAt}.");
                    sb.AppendLine();
                }
                sb.AppendLine("```csharp");
                sb.AppendLine(Cap(method.Body));
                sb.AppendLine("```");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns the business rules enforced by the application.
    /// </summary>
    [McpServerTool(Name = "xaf_rules")]
    [Description(
        "The business rules the application enforces: validation rules with their messages and " +
        "conditions, conditional appearance rules, and calculated properties. Use when asked what " +
        "the system requires, forbids, or computes. Optionally narrowed to one entity.")]
    public async Task<string> RulesAsync(
        [Description("Restrict to one entity. Omit for every rule in the application.")] string? entity = null,
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        var entities = string.IsNullOrWhiteSpace(entity)
            ? app.Entities
            : app.Entities.Where(e => e.ClassName.Equals(entity, StringComparison.OrdinalIgnoreCase)).ToList();

        if (entities.Count == 0)
            return NotFound("entity", entity!, app.Entities.Select(e => e.ClassName));

        // Asked about one entity, the answer is everything that governs it — a rule on a base is
        // enforced when this entity is saved, and the caller is standing in front of this entity.
        // Asked about the application, the answer is its rule set: each rule once, under the class
        // that wrote it. The same question at two scales wants two answers, which is the half of
        // issue #14 that was filed as debatable.
        var wholeApplication = string.IsNullOrWhiteSpace(entity);

        bool Governs(ExtractedEntity e) => wholeApplication
            ? e.ValidationRules.Any(r => r.InheritedFrom is null)
              || e.AppearanceRules.Any(r => r.InheritedFrom is null)
              || e.Properties.Any(p => p.InheritedFrom is null && !string.IsNullOrWhiteSpace(p.PersistentAlias))
            : e.ValidationRules.Count > 0
              || e.AppearanceRules.Count > 0
              || e.Properties.Any(p => !string.IsNullOrWhiteSpace(p.PersistentAlias));

        var relevant = entities.Where(Governs).ToList();

        if (relevant.Count == 0)
        {
            return wholeApplication
                ? $"{app.ProjectName} declares no validation rules, appearance rules or calculated properties."
                : $"Nothing validates, styles or calculates on `{entity}`, and it inherits no such rule either.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Business rules — {app.ProjectName}");

        foreach (var target in relevant)
        {
            sb.AppendLine();
            sb.AppendLine($"## {target.ClassName}");
            AppendRules(sb, target, declaredOnly: wholeApplication);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns Model Editor customizations.
    /// </summary>
    [McpServerTool(Name = "xaf_model")]
    [Description(
        "Model Editor (.xafml) customizations: captions, list and detail view settings, columns, " +
        "filters and application options. IMPORTANT — this behavior exists only in XML and cannot " +
        "be inferred from the C# at all, so check it before concluding how a screen behaves.")]
    public async Task<string> ModelAsync(
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        if (app.ModelEditorInfo is not { } model ||
            (model.BOModelClasses.Count == 0 && model.Views.Count == 0))
        {
            return $"{app.ProjectName} has no Model Editor customizations. Its UI behavior follows from " +
                   "the business classes and XAF defaults alone.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Model Editor customizations — {app.ProjectName}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(model.ApplicationTitle))
            sb.AppendLine($"Application title: \"{model.ApplicationTitle}\".");

        if (model.SourceFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Merged from {model.SourceFiles.Count} `.xafml` " +
                          $"{(model.SourceFiles.Count == 1 ? "file" : "files")}.");
        }

        if (model.BOModelClasses.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Class settings");
            sb.AppendLine();
            foreach (var boClass in model.BOModelClasses)
            {
                sb.Append($"- **{boClass.ClassName}**");
                if (!string.IsNullOrWhiteSpace(boClass.Caption)) sb.Append($" — caption \"{boClass.Caption}\"");
                if (boClass.IsCloneable) sb.Append(" — cloneable");
                sb.AppendLine();

                foreach (var attribute in boClass.CustomAttributes)
                    sb.AppendLine($"  - {attribute.Key} = {attribute.Value}");
            }
        }

        if (model.Views.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Views");
            sb.AppendLine();
            foreach (var view in model.Views)
                sb.AppendLine($"- **{view.Id}** ({view.ViewType})");
        }

        if (model.SchemaModules.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Schema modules");
            sb.AppendLine();
            foreach (var schema in model.SchemaModules)
                sb.AppendLine($"- {schema.Name}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns the editors that make screens differ from their property types.
    /// </summary>
    [McpServerTool(Name = "xaf_editors")]
    [Description(
        "Custom property and list editors this application defines, and built-in editors its " +
        "controllers reconfigure at run time. IMPORTANT — a property rendered by one of these does " +
        "NOT show the control its type implies, and the business class says nothing about it. " +
        "Check this before describing or changing how anything appears on screen.")]
    public async Task<string> EditorsAsync(
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);
        var customized = app.Controllers.Where(c => c.CustomizedEditors.Count > 0).ToList();

        if (app.Editors.Count == 0 && customized.Count == 0)
        {
            return $"{app.ProjectName} defines no custom editors, and no controller reconfigures a " +
                   "built-in one. Every property shows the control its type implies.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Custom editors — {app.ProjectName}");

        foreach (var editor in app.Editors.OrderBy(e => e.ClassName, StringComparer.Ordinal))
        {
            sb.AppendLine();
            sb.AppendLine($"## {editor.ClassName}");
            sb.AppendLine();
            sb.AppendLine($"- Kind: {editor.Kind}");

            if (!string.IsNullOrWhiteSpace(editor.TargetType))
                sb.AppendLine($"- Renders: `{editor.TargetType}`");

            if (editor.IsDefault)
                sb.AppendLine("- **Replaces the default editor for that type everywhere** — it changes screens nobody edited.");
            else if (!string.IsNullOrWhiteSpace(editor.Alias))
                sb.AppendLine($"- Requested with `[EditorAlias(\"{editor.Alias}\")]`, or assigned in the Model Editor.");

            if (!string.IsNullOrWhiteSpace(editor.BaseType))
                sb.AppendLine($"- Based on `{editor.BaseType}`");

            if (!string.IsNullOrWhiteSpace(editor.SourceProject))
                sb.AppendLine($"- Defined in `{editor.SourceProject}` — a platform project beside the module, not in it.");

            if (editor.UsedBy.Count > 0)
                sb.AppendLine($"- Used by: {string.Join(", ", editor.UsedBy)}");

            if (editor.ClientAssets.Count > 0)
                sb.AppendLine($"- Needs: {string.Join(", ", editor.ClientAssets)} — client-side files, behavior in neither C# nor XML.");

            if (!string.IsNullOrWhiteSpace(editor.Description))
            {
                sb.AppendLine();
                sb.AppendLine(editor.Description);
            }
        }

        if (customized.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Built-in editors reconfigured at run time");
            sb.AppendLine();
            sb.AppendLine("No custom editor class exists for these. A controller reaches into a built-in editor's");
            sb.AppendLine("component model, so nothing on the entity or in the Model Editor records it.");
            sb.AppendLine();

            foreach (var controller in customized.OrderBy(c => c.ClassName, StringComparer.Ordinal))
                sb.AppendLine($"- `{controller.ClassName}` changes {string.Join(", ", controller.CustomizedEditors)}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns what happened to the data between released versions.
    /// </summary>
    [McpServerTool(Name = "xaf_migrations")]
    [Description(
        "Updater blocks that ran once when an existing database was upgraded past a version, and " +
        "never again. Use when asked why a column contains what it contains, where legacy data " +
        "came from, or what changed between releases — the code running today cannot explain any " +
        "of that, and reasoning from it produces a plausible wrong answer.")]
    public async Task<string> MigrationsAsync(
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        if (app.Migrations.Count == 0)
        {
            return $"{app.ProjectName} has no version-gated migrations. Its updater only seeds a " +
                   "fresh database; no block runs conditionally on an upgrade, so no data was " +
                   "transformed by a past release.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Data migrations — {app.ProjectName}");
        sb.AppendLine();
        sb.AppendLine("Each runs **at most once** for any database, when it is upgraded past the version " +
                      "named, and never again after that.");

        foreach (var migration in app.Migrations.OrderBy(m => m.TargetVersion, StringComparer.Ordinal))
        {
            sb.AppendLine();
            sb.AppendLine($"## Upgrading to {migration.TargetVersion ?? "an unknown version"}");
            sb.AppendLine();

            sb.AppendLine(migration.Phase switch
            {
                MigrationPhase.BeforeSchemaUpdate =>
                    "- Ran **before** the schema changed, so the new columns did not exist yet.",
                MigrationPhase.AfterSchemaUpdate =>
                    "- Ran **after** the schema changed, so anything dropped was already gone.",
                _ => "- Schema phase could not be established.",
            });

            if (!string.IsNullOrWhiteSpace(migration.MinimumVersion))
                sb.AppendLine($"- Existing databases only, from {migration.MinimumVersion} upward.");

            sb.AppendLine($"- Condition: `{migration.Condition}`");

            if (migration.CallsMethods.Count > 0)
                sb.AppendLine($"- Calls: {string.Join(", ", migration.CallsMethods)}");

            if (!string.IsNullOrWhiteSpace(migration.Description))
            {
                sb.AppendLine();
                sb.AppendLine(migration.Description);
            }

            if (!string.IsNullOrWhiteSpace(migration.Code))
            {
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(Cap(migration.Code));
                sb.AppendLine("```");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns everything that runs on one screen.
    /// </summary>
    [McpServerTool(Name = "xaf_view")]
    [Description(
        "What is loaded when a screen opens: every controller XAF activates on that view, why each " +
        "one matches, and which of their actions appear. Call with no view to list the " +
        "application's screens. IMPORTANT — most views exist in no file at all; XAF generates them " +
        "from the business classes, so neither the C# nor the .xafml can be read to find them. Use " +
        "before changing a controller, adding an action, or answering what a screen does.")]
    public async Task<string> ViewAsync(
        [Description("View id, e.g. 'Order_ListView'. Omit to list every view. Case-insensitive.")]
        string? view = null,
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);

        if (app.Views.Count == 0)
        {
            return $"{app.ProjectName} has no views: no persistent business class was found, and " +
                   "XAF generates views from those.";
        }

        return string.IsNullOrWhiteSpace(view)
            ? ViewIndex(app)
            : OneView(app, view);
    }

    /// <summary>
    /// Lists the application's screens, grouped by the class they show.
    /// </summary>
    private static string ViewIndex(ExtractedProject app)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Views — {app.ProjectName}");
        sb.AppendLine();
        sb.AppendLine($"{app.Views.Count} views. XAF generates a list, detail and lookup view for every " +
                      "business class, plus a list view for every collection property, so most of these " +
                      "appear in no file. Ask for one by id to see what runs on it.");

        foreach (var group in app.Views
                     .GroupBy(v => v.ObjectType ?? "(no business class)")
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.AppendLine();
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var view in group.OrderBy(v => v.Id, StringComparer.Ordinal))
            {
                var mine = view.Activates.Count(a => !a.Framework);

                sb.Append($"- `{view.Id}` — {Describe(view)}");

                if (mine > 0)
                    sb.Append($", {mine} of this codebase's controller{(mine == 1 ? "" : "s")}");

                sb.AppendLine();
            }
        }

        AppendUndetermined(sb, app);

        return sb.ToString();
    }

    /// <summary>
    /// The photograph of one screen.
    /// </summary>
    private static string OneView(ExtractedProject app, string requested)
    {
        var view = app.Views.FirstOrDefault(v => v.Id.Equals(requested, StringComparison.OrdinalIgnoreCase));

        if (view is null)
            return NotFound("view", requested, app.Views.Select(v => v.Id));

        var sb = new StringBuilder();
        sb.AppendLine($"# {view.Id}");
        sb.AppendLine();
        sb.AppendLine(Describe(view) + ".");

        if (!string.IsNullOrWhiteSpace(view.Caption))
            sb.AppendLine($"\nCaption: \"{view.Caption}\".");

        if (view.OwnerProperty is { } owner)
            sb.AppendLine($"\nShown by `{view.OwnerEntity}.{owner}`, inside that class's detail view.");

        sb.AppendLine();
        sb.AppendLine(view.Origin switch
        {
            ViewOrigin.Generated =>
                "Generated by XAF from the business class. It exists in no file — searching the " +
                "source tree for this id finds nothing, and that is not evidence it is missing.",
            ViewOrigin.Customized =>
                "Generated by XAF, then customized in the Model Editor. The `.xafml` holds only the " +
                "differences.",
            _ => "Defined in the Model Editor. It has no generated counterpart.",
        });

        var mine = view.Activates.Where(a => !a.Framework).ToList();
        var framework = view.Activates.Where(a => a.Framework).ToList();

        sb.AppendLine();
        sb.AppendLine($"## Written by this team ({mine.Count})");

        if (mine.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("None. No controller in this codebase activates on this view.");
        }

        foreach (var activation in mine)
        {
            sb.AppendLine();
            sb.Append($"### {activation.Controller}");

            if (!string.IsNullOrWhiteSpace(activation.SourceProject))
                sb.Append($" ({activation.SourceProject})");

            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(activation.Reasons.Count == 0
                ? "- Restricts nothing, so it runs on **every view in the application**."
                : "- Matches because " +
                  string.Join(", and ", activation.Reasons.Select(Core.Generators.ActivationReasonText.English)) +
                  ".");

            if (activation.Replaces.Count > 0)
            {
                sb.AppendLine($"- **Replaces {string.Join(", ", activation.Replaces.Select(name => $"`{name}`"))}** " +
                              "— XAF activates only the most derived controller of a chain, so the " +
                              "original is switched off application-wide.");
            }

            if (activation.Actions.Count > 0)
            {
                sb.AppendLine("- Actions here:");
                foreach (var action in activation.Actions)
                    sb.AppendLine($"  - {action}");
            }
        }

        // The framework layer is kept compact on purpose: it is inherited behaviour, there is a lot
        // of it, and giving each entry the same weight as the team's own would bury the two lines
        // someone actually came to read.
        if (framework.Count > 0 || app.FrameworkAlwaysActive.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Provided by XAF ({framework.Count} specific to this view)");
            sb.AppendLine();
            sb.AppendLine("Inherited behaviour from the registered modules. It is not in this codebase " +
                          "and cannot be changed here — but it is why the view has the actions it has.");

            if (framework.Count > 0)
                sb.AppendLine();

            foreach (var activation in framework.OrderBy(a => a.Controller, StringComparer.Ordinal))
            {
                sb.Append($"- `{activation.Controller}` ({activation.SourceProject})");

                if (!string.IsNullOrWhiteSpace(activation.Summary))
                    sb.Append($" — {XafDiscoveryTools.Compact(activation.Summary)}");

                sb.AppendLine();
            }

            if (app.FrameworkAlwaysActive.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Plus {app.FrameworkAlwaysActive.Count} framework controllers that restrict " +
                              "nothing and therefore load onto every view in the application: " +
                              string.Join(", ", app.FrameworkAlwaysActive.Select(name => $"`{name}`")) + ".");
            }
        }
        else if (app.CatalogVersion is null)
        {
            sb.AppendLine();
            sb.AppendLine("## Provided by XAF");
            sb.AppendLine();
            sb.AppendLine("Not known. XAF's own controllers run here too, and naming them needs the " +
                          "ground-truth catalog — run `xaflogic catalog build` on a machine with a " +
                          "DevExpress licence.");
        }

        sb.AppendLine();
        sb.AppendLine("## What this cannot tell you");
        sb.AppendLine();
        sb.AppendLine("A controller that passes all four targeting conditions can still switch itself " +
                      "off at run time through `Active[\"reason\"] = …`, which depends on data and on " +
                      "the user. This is what XAF **loads** onto the view, not what will necessarily " +
                      "do something.");

        AppendUndetermined(sb, app);

        return sb.ToString();
    }

    /// <summary>
    /// Names the controllers that belong to some view nobody can name.
    /// </summary>
    private static void AppendUndetermined(StringBuilder sb, ExtractedProject app)
    {
        var undetermined = Core.Analyzers.ViewActivationResolver.Undetermined(app.Controllers).ToList();

        if (undetermined.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("## Where these run could not be established");
        sb.AppendLine();
        sb.AppendLine("Listed against no view above, because something about where they activate cannot be " +
                      "read from the source. They are real, and they are restricted — claiming them on every " +
                      "screen would be the bigger error:");
        sb.AppendLine();

        foreach (var controller in undetermined)
        {
            sb.AppendLine($"- `{controller.ClassName}` — " +
                          Core.Analyzers.ViewActivationResolver.UndeterminedReason(controller));
        }
    }

    /// <summary>One line describing what kind of view this is.</summary>
    private static string Describe(ExtractedView view)
    {
        var kind = view.ViewType switch
        {
            ModelViewType.ListView when view.Id.EndsWith("_LookupListView", StringComparison.Ordinal) =>
                "lookup list",
            ModelViewType.ListView => "list",
            ModelViewType.DetailView => "detail",
            _ => "dashboard",
        };

        var where = view.Nesting switch
        {
            ViewNesting.Root => "root",
            ViewNesting.Nested => "nested",
            _ => "root or nested",
        };

        var navigation = view.InNavigation ? ", in navigation" : "";

        return $"{kind} view, {where}{navigation}";
    }

    /// <summary>Writes an entity's validation, appearance and calculation rules.</summary>
    /// <param name="sb">The buffer being written to.</param>
    /// <param name="entity">The entity whose rules are written.</param>
    /// <param name="declaredOnly">
    /// Leaves out what the entity inherits, for a caller listing the whole application rather than
    /// reading one entity.
    /// </param>
    private static void AppendRules(StringBuilder sb, ExtractedEntity entity, bool declaredOnly = false)
    {
        bool Wanted(string? inheritedFrom) => !declaredOnly || inheritedFrom is null;

        var validation = entity.ValidationRules
            .Where(r => Wanted(r.InheritedFrom))
            .OrderByDescending(r => r.InheritedFrom is null)
            .ToList();

        var appearance = entity.AppearanceRules
            .Where(r => Wanted(r.InheritedFrom))
            .OrderByDescending(r => r.InheritedFrom is null)
            .ToList();

        if (validation.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Validation");
            sb.AppendLine();
            foreach (var rule in validation)
            {
                sb.Append($"- **{rule.RuleType}**");
                if (!string.IsNullOrWhiteSpace(rule.TargetProperty)) sb.Append($" on `{rule.TargetProperty}`");
                if (!string.IsNullOrWhiteSpace(rule.Id)) sb.Append($" — `{rule.Id}`");
                if (rule.InheritedFrom is { Length: > 0 } from) sb.Append($" (inherited from `{from}`)");
                sb.AppendLine();
                // What the rule enforces. It was read and then shown nowhere, so an agent asking
                // this tool for the rules got the message and never the condition behind it.
                if (!string.IsNullOrWhiteSpace(rule.Expression)) sb.AppendLine($"  - must hold: `{rule.Expression}`");
                if (!string.IsNullOrWhiteSpace(rule.TargetCriteria)) sb.AppendLine($"  - applies when: `{rule.TargetCriteria}`");
                if (!string.IsNullOrWhiteSpace(rule.MessageTemplate)) sb.AppendLine($"  - message: \"{XafDiscoveryTools.Compact(rule.MessageTemplate)}\"");
                // Only when it is not the ordinary save: everywhere else it is noise, and here it
                // is the reason the rule does not fire where the reader expects it to.
                if (rule.Contexts is { Length: > 0 } and not ("DefaultContexts.Save" or "Save"))
                    sb.AppendLine($"  - validation context: `{rule.Contexts}`");
            }
        }

        if (appearance.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Conditional appearance");
            sb.AppendLine();
            foreach (var rule in appearance)
            {
                var declarer = rule.InheritedFrom is { Length: > 0 } from ? $" (inherited from `{from}`)" : "";
                // An unnamed rule is ordinary rather than an omission -- a rule written on a property
                // already says what it governs -- and it printed as an empty bold span, `****`.
                var name = rule.Id is { Length: > 0 } id ? $"**{id}**" : "unnamed rule";
                // Naming the kind, because "on Delete" reads as a column and the rule may well be
                // governing the Delete *action*. XAF defaults the type to ViewItem, so silence here
                // means a field and the common case says nothing extra.
                var target = rule.TargetItems is { Length: > 0 } items
                    ? rule.AppearanceItemType switch
                    {
                        "Action" => $"{items} (actions)",
                        "LayoutItem" => $"{items} (layout items)",
                        _ => items,
                    }
                    : "the whole object";

                sb.AppendLine($"- {name} on {target}{declarer}");
                // Said outright rather than by omission: a rule that declares no criteria is always
                // active, and a reader met only by a missing line cannot tell that from a criteria
                // that failed to extract.
                sb.AppendLine(string.IsNullOrWhiteSpace(rule.Criteria)
                    ? "  - applies: always"
                    : $"  - when: `{rule.Criteria}`");
                if (!string.IsNullOrWhiteSpace(rule.Visibility)) sb.AppendLine($"  - visibility: {rule.Visibility}");
                if (!string.IsNullOrWhiteSpace(rule.Enabled)) sb.AppendLine($"  - enabled: {rule.Enabled}");
                if (!string.IsNullOrWhiteSpace(rule.BackColor)) sb.AppendLine($"  - back colour: {rule.BackColor}");
                if (!string.IsNullOrWhiteSpace(rule.FontColor)) sb.AppendLine($"  - font colour: {rule.FontColor}");
            }
        }

        var calculated = entity.Properties
            .Where(p => Wanted(p.InheritedFrom) && !string.IsNullOrWhiteSpace(p.PersistentAlias))
            .OrderByDescending(p => p.InheritedFrom is null)
            .ToList();

        if (calculated.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Calculated properties");
            sb.AppendLine();
            foreach (var property in calculated)
            {
                var declarer = property.InheritedFrom is { Length: > 0 } from ? $" (inherited from `{from}`)" : "";
                sb.AppendLine($"- `{property.Name}` = `{property.PersistentAlias}`{declarer}");
            }
        }
    }

    /// <summary>
    /// Explains that something does not exist, and says what does.
    /// </summary>
    /// <remarks>
    /// The wording matters. "Not found" invites an agent to assume it simply looked in the wrong
    /// place and to invent the thing anyway. Extraction covers the whole tree, so absence is a
    /// fact worth stating as one.
    /// </remarks>
    private static string NotFound(string kind, string requested, IEnumerable<string> available)
    {
        var known = available.OrderBy(a => a, StringComparer.Ordinal).ToList();

        // "entity" does not pluralize by adding an s, and "19 entitys" in an answer an agent is
        // about to quote undermines everything else the response says.
        var plural = kind switch
        {
            "entity" => "entities",
            _ => kind + "s",
        };

        var sb = new StringBuilder();
        sb.AppendLine($"There is no {kind} called '{requested}' in this application.");
        sb.AppendLine();
        sb.AppendLine($"This is the complete list of {known.Count} " +
                      $"{(known.Count == 1 ? kind : plural)}, extracted from the whole source tree:");
        sb.AppendLine();
        foreach (var item in known)
            sb.AppendLine($"- {item}");
        sb.AppendLine();
        sb.AppendLine($"If the user expects '{requested}' to exist, it has not been created yet.");

        return sb.ToString();
    }

    /// <summary>
    /// Where a declaration is, for an agent whose next move is to open the file.
    /// </summary>
    /// <remarks>
    /// The full path rather than one relative to the project: the server runs beside the source it
    /// read, so an absolute path is directly openable and needs no root to resolve against. Absent
    /// when there is no line to give, because a citation nobody can follow is worse than none.
    /// </remarks>
    private static string At(string filePath, int line) =>
        string.IsNullOrWhiteSpace(filePath) || line <= 0 ? "" : $"`{filePath}:{line}`";

    /// <summary>
    /// Where a member is, given that its container has already been cited.
    /// </summary>
    /// <remarks>
    /// A member almost always sits in the file its class was cited at, and repeating a hundred
    /// characters of path for every action and every helper method spends an agent's context on
    /// nothing — the same reason <see cref="MaxCodeLength"/> exists. So the shared case says only
    /// the line. A partial class is the case that is not shared, and there the full path is printed
    /// precisely because the reader would otherwise open the wrong file.
    /// </remarks>
    private static string Within(string containerPath, string filePath, int line)
    {
        if (string.IsNullOrWhiteSpace(filePath) || line <= 0)
            return "";

        return filePath.Equals(containerPath, StringComparison.OrdinalIgnoreCase)
            ? $"line {line}"
            : $"`{filePath}:{line}`";
    }

    /// <summary>Caps a code block, saying so rather than truncating silently.</summary>
    private static string Cap(string code) =>
        code.Length <= MaxCodeLength
            ? code
            : code[..MaxCodeLength] + $"\n\n// … truncated at {MaxCodeLength} characters; read the source file for the rest.";
}
