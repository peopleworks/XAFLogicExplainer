using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Mcp.Tools;

/// <summary>
/// Tools for finding out what an application contains.
/// </summary>
/// <remarks>
/// These are the entry points. An agent that has just been asked about an unfamiliar XAF codebase
/// starts here, then follows up with the detail tools for whatever it found.
/// </remarks>
[McpServerToolType]
public sealed class XafDiscoveryTools
{
    private readonly XafProjectContext _context;

    /// <summary>Creates the tool set.</summary>
    public XafDiscoveryTools(XafProjectContext context) => _context = context;

    /// <summary>
    /// Summarizes the application, including the complete inventory of what it contains.
    /// </summary>
    [McpServerTool(Name = "xaf_overview")]
    [Description(
        "What this XAF application is and everything it contains: ORM, module setup, and the " +
        "COMPLETE list of entities, controllers, actions and navigation groups. Call this first " +
        "when asked anything about the application as a whole. The lists are exhaustive: if " +
        "something is not in them, it does not exist in this application.")]
    public async Task<string> OverviewAsync(
        [Description("Project name, when several are configured. Omit for the default.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var app = await _context.GetAsync(project, cancellationToken);
        var sb = new StringBuilder();

        var actionCount = app.Controllers.Sum(c => c.Actions.Count);
        var orm = Orm.Label(app.OrmType);

        sb.AppendLine($"# {app.ProjectName}");
        sb.AppendLine();
        sb.Append($"DevExpress XAF application. Persistence: **{orm}**.");

        // Omitted rather than printed empty. "Target framework: ." is not a smaller answer than
        // naming one, it is an unreadable one, and this string is what an agent reasons from.
        if (!string.IsNullOrWhiteSpace(app.TargetFramework))
            sb.Append($" Target framework: {app.TargetFramework}.");

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine($"- Entities: **{app.Entities.Count}**");
        sb.AppendLine($"- Controllers: **{app.Controllers.Count}** exposing **{actionCount}** actions");
        sb.AppendLine($"- Navigation groups: {app.Navigation.Count}");
        sb.AppendLine($"- Seed data methods: {app.SeedData.Count}");

        if (app.ModelEditorInfo is { } model)
            sb.AppendLine($"- Model Editor: {model.BOModelClasses.Count} class settings, {model.Views.Count} views");

        sb.AppendLine();
        sb.AppendLine("These lists are complete, not sampled. Anything absent from them does not exist here.");
        sb.AppendLine();

        if (app.ModuleInfo is { } module)
        {
            sb.AppendLine($"## Module: `{module.ModuleClassName}`");
            sb.AppendLine();
            if (module.RequiredModules.Count > 0)
            {
                sb.AppendLine("Required XAF modules:");
                foreach (var required in module.RequiredModules.OrderBy(m => m, StringComparer.Ordinal))
                    sb.AppendLine($"- {required}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Entities");
        sb.AppendLine();
        foreach (var entity in app.Entities.OrderBy(e => e.ClassName, StringComparer.Ordinal))
        {
            var summary = entity.Description ?? entity.ModelCaption;
            sb.Append($"- **{entity.ClassName}** (`{entity.BaseType}`, {entity.Properties.Count} properties)");
            sb.AppendLine(string.IsNullOrWhiteSpace(summary) ? "" : $" — {Compact(summary)}");
        }

        sb.AppendLine();
        sb.AppendLine("## Controllers and actions");
        sb.AppendLine();
        foreach (var controller in app.Controllers.OrderBy(c => c.ClassName, StringComparer.Ordinal))
        {
            var target = string.IsNullOrWhiteSpace(controller.TargetObjectType)
                ? "any view"
                : controller.TargetObjectType;

            sb.AppendLine($"- **{controller.ClassName}** → {target}");

            foreach (var action in controller.Actions)
            {
                var caption = string.IsNullOrWhiteSpace(action.Caption) ? action.ActionId : action.Caption;
                sb.AppendLine($"  - action `{action.ActionId}`: {Compact(caption)}");
            }
        }

        if (app.Navigation.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Navigation");
            sb.AppendLine();
            foreach (var group in app.Navigation)
                sb.AppendLine($"- **{group.GroupName}**: {string.Join(", ", group.EntityClassNames)}");
        }

        sb.AppendLine();
        sb.AppendLine("Use `xaf_entity`, `xaf_controller`, `xaf_rules` or `xaf_model` for detail on any of these.");

        return sb.ToString();
    }

    /// <summary>
    /// Finds where something is defined or referenced.
    /// </summary>
    [McpServerTool(Name = "xaf_search")]
    [Description(
        "Search the whole application for a term: entity and property names, controllers, actions, " +
        "validation messages, criteria expressions and seed data. Use when you know roughly what " +
        "you are looking for but not where it lives — for example a field name mentioned by the " +
        "user, or a business concept like 'discount'. Returns what matched and where.")]
    public async Task<string> SearchAsync(
        [Description("Text to look for. Case-insensitive, matches substrings.")] string query,
        [Description("Optional filter: entity, property, controller, action, rule, or seed.")] string? kind = null,
        [Description("Project name, when several are configured.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Provide a search term.";

        var app = await _context.GetAsync(project, cancellationToken);
        var hits = new List<string>();

        bool Wanted(string k) => kind is null || kind.Equals(k, StringComparison.OrdinalIgnoreCase);
        bool Matches(string? text) => text is not null && text.Contains(query, StringComparison.OrdinalIgnoreCase);

        foreach (var entity in app.Entities)
        {
            if (Wanted("entity") && (Matches(entity.ClassName) || Matches(entity.Description)))
                hits.Add($"**entity** `{entity.ClassName}` — {entity.Properties.Count} properties, base `{entity.BaseType}`");

            if (Wanted("property"))
            {
                // Where declared, for the same reason as the rules below: since entities carry the
                // columns they inherit, searching an audit base's column name returned one hit per
                // class in the application and buried everything else in the result.
                foreach (var property in entity.Properties.Where(p =>
                             p.InheritedFrom is null && (Matches(p.Name) || Matches(p.Description))))
                {
                    var computed = string.IsNullOrWhiteSpace(property.PersistentAlias)
                        ? ""
                        : $", calculated: `{Compact(property.PersistentAlias)}`";
                    hits.Add($"**property** `{entity.ClassName}.{property.Name}` — {property.TypeName}{computed}");
                }
            }

            if (Wanted("rule"))
            {
                // Searched by identifier and by the condition too: a rule is usually looked for by
                // the name an error message carried, or by the column an expression mentions.
                //
                // Where declared, so a search returns the rule and not the class hierarchy under
                // it: one rule on an audit base would otherwise fill the results by itself.
                foreach (var rule in entity.ValidationRules.Where(r =>
                             r.InheritedFrom is null &&
                             (Matches(r.RuleType) || Matches(r.TargetProperty) ||
                              Matches(r.MessageTemplate) || Matches(r.TargetCriteria) ||
                              Matches(r.Id) || Matches(r.Expression))))
                {
                    var says = rule.MessageTemplate ?? rule.Expression;
                    hits.Add($"**rule** `{entity.ClassName}` {rule.RuleType} on `{rule.TargetProperty}` — {Compact(says)}");
                }
            }
        }

        foreach (var controller in app.Controllers)
        {
            if (Wanted("controller") && (Matches(controller.ClassName) || Matches(controller.BusinessLogicSummary)))
                hits.Add($"**controller** `{controller.ClassName}` — targets {controller.TargetObjectType ?? "any view"}");

            if (Wanted("action"))
            {
                foreach (var action in controller.Actions.Where(a =>
                             Matches(a.ActionId) || Matches(a.Caption) ||
                             Matches(a.ExecuteMethodBody) || Matches(a.BusinessLogicSummary)))
                {
                    hits.Add($"**action** `{action.ActionId}` in `{controller.ClassName}` — {Compact(action.Caption ?? action.ActionId)}");
                }
            }
        }

        if (Wanted("seed"))
        {
            foreach (var seed in app.SeedData.Where(s =>
                         Matches(s.EntityType) || Matches(s.MethodName) || Matches(s.RawSourceCode)))
            {
                hits.Add($"**seed** `{seed.MethodName}` creates `{seed.EntityType}` ({seed.Records.Count} records)");
            }
        }

        if (hits.Count == 0)
        {
            return $"No match for '{query}' in {app.ProjectName}.\n\n" +
                   "The extraction covers the whole source tree, so this term genuinely does not appear " +
                   "in the application's entities, controllers, actions, rules or seed data. " +
                   "Call `xaf_overview` to see what does exist.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"{hits.Count} {(hits.Count == 1 ? "match" : "matches")} for '{query}' in {app.ProjectName}:");
        sb.AppendLine();
        foreach (var hit in hits.Take(60))
            sb.AppendLine($"- {hit}");

        if (hits.Count > 60)
            sb.AppendLine($"\n({hits.Count - 60} further matches not shown — narrow the query or pass `kind`.)");

        return sb.ToString();
    }

    /// <summary>
    /// Forces the next question to re-read the source.
    /// </summary>
    [McpServerTool(Name = "xaf_refresh")]
    [Description(
        "Discard cached analysis and re-read the source on the next query. The server already " +
        "detects file changes automatically, so this is only needed if you believe the cached " +
        "view is wrong.")]
    public async Task<string> RefreshAsync(
        [Description("Project name, or omit to refresh all configured projects.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var dropped = await _context.InvalidateAsync(project, cancellationToken);
        return dropped == 0
            ? "Nothing was cached; the next query reads from source anyway."
            : $"Discarded {dropped} cached {(dropped == 1 ? "project" : "projects")}. The next query re-reads the source.";
    }

    /// <summary>Flattens text to a single line for list output.</summary>
    internal static string Compact(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var single = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (single.Contains("  ", StringComparison.Ordinal))
            single = single.Replace("  ", " ", StringComparison.Ordinal);

        return single.Length > 200 ? single[..200] + "…" : single;
    }
}
