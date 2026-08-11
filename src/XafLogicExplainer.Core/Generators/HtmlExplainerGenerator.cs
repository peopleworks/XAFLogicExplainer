using System.Globalization;
using System.Net;
using System.Text;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Renders an extracted application as a single self-contained web page.
/// </summary>
/// <remarks>
/// The same extraction serves two readers. An agent reads <c>AGENTS.md</c> or queries the MCP
/// server; a <em>person</em> — someone who has just inherited a ten-year-old XAF application, or
/// who has to explain one to a client — needs the same knowledge arranged very differently.
/// <para>
/// One file, no dependencies, no server. An explainer has to survive being emailed, opened from a
/// network share, and read on a machine with no internet, because that is how handovers actually
/// happen.
/// </para>
/// </remarks>
public sealed class HtmlExplainerGenerator
{
    /// <summary>Longest method body reproduced before it is cut.</summary>
    private const int MaxCodeLength = 8000;

    private readonly string _toolVersion;

    /// <summary>Creates a generator.</summary>
    /// <param name="toolVersion">Stamped into the footer so a stale page can be identified.</param>
    public HtmlExplainerGenerator(string toolVersion = "0.10.1") => _toolVersion = toolVersion;

    /// <summary>
    /// Renders the whole page.
    /// </summary>
    /// <param name="project">The extracted application.</param>
    public string Generate(ExtractedProject project)
    {
        var sb = new StringBuilder();
        var graph = EntityGraph.Build(project);

        sb.AppendLine("<!doctype html>");
        sb.AppendLine($"<html lang=\"en\">");
        WriteHead(sb, project);
        sb.AppendLine("<body>");

        WriteHeader(sb, project);
        WriteNav(sb, project);

        sb.AppendLine("<main class=\"wrap\">");
        WriteMap(sb, graph);
        WriteEntities(sb, project);
        WriteOperations(sb, project);
        WriteScreens(sb, project);
        WriteRules(sb, project);
        WriteCriteria(sb, project);
        WriteModelEditor(sb, project);
        WriteEditors(sb, project);
        WriteSeedData(sb, project);
        WriteMigrations(sb, project);
        sb.AppendLine("</main>");

        WriteFooter(sb, project);

        sb.AppendLine($"<script>{HtmlExplainerStyles.Js}</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    // ------------------------------------------------------------------ head

    private static void WriteHead(StringBuilder sb, ExtractedProject project)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{E(project.ProjectName)} — how this XAF application works</title>");
        sb.AppendLine($"<meta name=\"generator\" content=\"XAF Logic Explainer\">");
        // Inline, so a page opened from disk or a share does not ask a server for an icon it has
        // no way to provide.
        sb.AppendLine("<link rel=\"icon\" href=\"data:image/svg+xml,"
                    + "%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E"
                    + "%3Crect width='32' height='32' rx='7' fill='%230a0d12'/%3E"
                    + "%3Cpath d='M8 11h16M8 16h11M8 21h13' stroke='%23ff8a3d' stroke-width='2.6' stroke-linecap='round'/%3E"
                    + "%3C/svg%3E\">");
        sb.AppendLine($"<style>{HtmlExplainerStyles.Css}</style>");
        sb.AppendLine("</head>");
    }

    private static void WriteHeader(StringBuilder sb, ExtractedProject project)
    {
        var actions = project.Controllers.Sum(c => c.Actions.Count);
        var orm = IsEfCore(project.OrmType) ? "Entity Framework Core" : "XPO";
        var rules = project.Entities.Sum(e => e.ValidationRules.Count + e.AppearanceRules.Count);

        sb.AppendLine("<header><div class=\"wrap\">");
        sb.AppendLine("  <div class=\"head\">");
        sb.AppendLine("    <div class=\"head__id\">");
        sb.AppendLine($"      <h1>{E(project.ProjectName)}</h1>");
        sb.Append("      <p class=\"head__sub\">A DevExpress XAF application, persisted with ");
        sb.Append($"<strong>{orm}</strong>");
        if (!string.IsNullOrWhiteSpace(project.TargetFramework))
            sb.Append($" on {E(project.TargetFramework)}");
        sb.AppendLine(". Read from source — nothing here was written by hand.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"head__tools\">");
        sb.AppendLine("      <input id=\"q\" type=\"search\" placeholder=\"Search entities, actions, rules…\" aria-label=\"Search\">");
        sb.AppendLine("      <button class=\"iconbtn\" id=\"theme\" type=\"button\" aria-label=\"Switch light or dark\">◐</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div id=\"count\"></div>");

        sb.AppendLine("  <div class=\"stats\">");
        Stat(sb, project.Entities.Count, "entities");
        Stat(sb, project.Controllers.Count, "controllers");
        Stat(sb, actions, "actions");
        // Named for what they are. "9 rules" reads as a total, and it counted two of the six kinds
        // of rule this page goes on to document.
        Stat(sb, project.Entities.Sum(e => e.ValidationRules.Count), "validation rules");
        Stat(sb, project.Entities.Sum(e => e.AppearanceRules.Count), "appearance rules");
        if (project.Navigation.Count > 0) Stat(sb, project.Navigation.Count, "nav groups");
        if (project.SeedData.Count > 0) Stat(sb, project.SeedData.Count, "seed methods");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div></header>");
    }

    private static void Stat(StringBuilder sb, int value, string label) =>
        sb.AppendLine($"    <div class=\"stat\"><b>{value}</b><span>{label}</span></div>");

    private static void WriteNav(StringBuilder sb, ExtractedProject project)
    {
        sb.AppendLine("<nav><div class=\"wrap\"><ul>");
        sb.AppendLine("  <li><a href=\"#map\">Map</a></li>");
        sb.AppendLine("  <li><a href=\"#entities\">Entities</a></li>");
        sb.AppendLine("  <li><a href=\"#operations\">Operations</a></li>");
        if (project.Views.Count > 0) sb.AppendLine("  <li><a href=\"#screens\">Screens</a></li>");
        sb.AppendLine("  <li><a href=\"#rules\">Rules</a></li>");
        sb.AppendLine("  <li><a href=\"#criteria\">Criteria</a></li>");
        if (project.ModelEditorInfo is not null) sb.AppendLine("  <li><a href=\"#model\">Model Editor</a></li>");
        if (project.Editors.Count > 0) sb.AppendLine("  <li><a href=\"#editors\">Custom editors</a></li>");
        if (project.SeedData.Count > 0) sb.AppendLine("  <li><a href=\"#seed\">Seed data</a></li>");
        if (project.Migrations.Count > 0) sb.AppendLine("  <li><a href=\"#migrations\">Migrations</a></li>");
        sb.AppendLine("</ul></div></nav>");
    }

    // ------------------------------------------------------------------- map

    private static void WriteMap(StringBuilder sb, EntityGraph graph)
    {
        sb.AppendLine("<section id=\"map\">");
        sb.AppendLine("  <h2>The domain model</h2>");

        if (graph.IsEmpty)
        {
            sb.AppendLine("  <p class=\"lede empty\">No persistent classes were found.</p>");
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <p class=\"lede\">Every relationship this application declares. Hover an entity to isolate what it touches; click to jump to it.</p>");
        sb.AppendLine("  <div class=\"map\" id=\"map-figure\">");
        sb.AppendLine($"  <svg viewBox=\"0 0 {N(graph.Width)} {N(graph.Height)}\" role=\"img\" aria-label=\"Entity relationship map\">");

        // Edges first so nodes sit above them.
        foreach (var edge in graph.Edges)
        {
            var owned = edge.IsAggregated ? " own" : "";
            sb.AppendLine(
                $"    <path class=\"edge{owned}\" data-from=\"{E(edge.From.Name)}\" data-to=\"{E(edge.To.Name)}\" " +
                $"d=\"{Curve(edge, graph)}\"><title>{E(edge.From.Name)}.{E(edge.Label)} → {E(edge.To.Name)}" +
                $"{(edge.IsAggregated ? " (owned)" : "")}</title></path>");
        }

        foreach (var node in graph.Nodes)
        {
            // Labels sit outside the ring, anchored away from the centre so they never overlap it.
            var outward = node.X < graph.Width / 2 - 1 ? "end" : node.X > graph.Width / 2 + 1 ? "start" : "middle";
            var labelX = node.X + Math.Cos(node.Angle) * (node.Radius + 9);
            var labelY = node.Y + Math.Sin(node.Angle) * (node.Radius + 9) + 4;

            sb.AppendLine($"    <g class=\"node\" data-name=\"{E(node.Name)}\">");
            sb.AppendLine($"      <circle cx=\"{N(node.X)}\" cy=\"{N(node.Y)}\" r=\"{N(node.Radius)}\">" +
                          $"<title>{E(node.Name)} — {node.PropertyCount} properties, {node.Degree} relationships</title></circle>");
            sb.AppendLine($"      <text x=\"{N(labelX)}\" y=\"{N(labelY)}\" text-anchor=\"{outward}\">{E(node.Name)}</text>");
            sb.AppendLine("    </g>");
        }

        sb.AppendLine("  </svg>");
        sb.AppendLine("  <div class=\"legend\"><span><i></i>relationship</span><span><i class=\"own\"></i>owned — deleting the parent deletes it</span></div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Draws an edge as an arc bowed toward the centre.
    /// </summary>
    /// <remarks>
    /// Straight chords across a circle all pass through the middle and pile into an unreadable
    /// knot. Bowing each one inward by a fraction of its own length keeps short hops close to the
    /// rim and lets long ones separate.
    /// </remarks>
    private static string Curve(GraphEdge edge, EntityGraph graph)
    {
        var cx = graph.Width / 2;
        var cy = graph.Height / 2;

        var midX = (edge.From.X + edge.To.X) / 2;
        var midY = (edge.From.Y + edge.To.Y) / 2;

        // Pull the control point toward the centre, more for longer chords.
        var dx = edge.To.X - edge.From.X;
        var dy = edge.To.Y - edge.From.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        var pull = Math.Clamp(length / Math.Max(graph.Width, 1) * 0.75, .12, .55);

        var ctrlX = midX + (cx - midX) * pull;
        var ctrlY = midY + (cy - midY) * pull;

        return $"M{N(edge.From.X)},{N(edge.From.Y)} Q{N(ctrlX)},{N(ctrlY)} {N(edge.To.X)},{N(edge.To.Y)}";
    }

    // -------------------------------------------------------------- entities

    private static void WriteEntities(StringBuilder sb, ExtractedProject project)
    {
        sb.AppendLine("<section id=\"entities\">");
        sb.AppendLine($"  <h2>Business entities <span class=\"card__meta\">{project.Entities.Count}</span></h2>");
        sb.AppendLine("  <p class=\"lede\">Everything the application stores. Markers show what each property is: a key, required, or calculated by the database rather than in C#.</p>");

        foreach (var entity in project.Entities.OrderBy(e => e.ClassName, StringComparer.Ordinal))
        {
            var haystack = Haystack(entity.ClassName, entity.Description, entity.BaseType,
                string.Join(" ", entity.Properties.Select(p => p.Name + " " + p.TypeName)),
                string.Join(" ", entity.Relationships.Select(r => r.RelatedEntity)));

            sb.AppendLine($"  <article class=\"card\" id=\"entity-{E(entity.ClassName)}\" data-search=\"{haystack}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(entity.ClassName)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{E(entity.BaseType)} · {entity.Properties.Count} properties</span>");
            if (!string.IsNullOrWhiteSpace(entity.ModelCaption))
                sb.AppendLine($"      <span class=\"pill\">shown as “{E(entity.ModelCaption)}”</span>");
            sb.AppendLine("    </div>");

            if (!string.IsNullOrWhiteSpace(entity.Description))
                sb.AppendLine($"    <p class=\"card__desc\">{E(entity.Description)}</p>");

            if (entity.Properties.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Property</th><th>Type</th><th></th><th>Notes</th></tr></thead><tbody>");
                foreach (var property in entity.Properties)
                {
                    sb.Append($"      <tr><td class=\"mono\">{E(property.Name)}</td><td class=\"mono t\">{E(property.TypeName)}</td><td>");
                    if (property.IsKey) sb.Append("<span class=\"pill pill--key\">key</span> ");
                    if (property.IsRequired) sb.Append("<span class=\"pill pill--req\">required</span> ");
                    if (property.IsUnique) sb.Append("<span class=\"pill pill--req\">unique</span> ");
                    if (!string.IsNullOrWhiteSpace(property.PersistentAlias)) sb.Append("<span class=\"pill pill--calc\">calculated</span> ");
                    if (property.IsCollection) sb.Append("<span class=\"pill\">collection</span> ");
                    sb.Append("</td><td class=\"t\">");

                    var notes = new List<string>();
                    if (!string.IsNullOrWhiteSpace(property.PersistentAlias))
                        notes.Add($"= <code class=\"crit\">{E(property.PersistentAlias)}</code>");
                    if (!string.IsNullOrWhiteSpace(property.DataSourceCriteria))
                        notes.Add($"lookup filtered by <code class=\"crit\">{E(property.DataSourceCriteria)}</code>");
                    if (property.Size is > 0) notes.Add($"max {property.Size}");
                    if (!string.IsNullOrWhiteSpace(property.Description)) notes.Add(E(property.Description));

                    sb.AppendLine($"{string.Join(" · ", notes)}</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            if (entity.Relationships.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Relationship</th><th>To</th><th></th></tr></thead><tbody>");
                foreach (var relationship in entity.Relationships)
                {
                    sb.Append($"      <tr><td class=\"mono\">{E(relationship.PropertyName)}</td>");
                    sb.Append($"<td class=\"mono\"><a href=\"#entity-{E(relationship.RelatedEntity)}\">{E(relationship.RelatedEntity)}</a></td><td>");
                    sb.Append($"<span class=\"pill\">{Describe(relationship.Type)}</span> ");
                    if (relationship.IsAggregated) sb.Append("<span class=\"pill pill--own\">owned</span> ");
                    if (!string.IsNullOrWhiteSpace(relationship.AssociationName))
                        sb.Append($"<span class=\"pill\">{E(relationship.AssociationName)}</span>");
                    sb.AppendLine("</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    // ------------------------------------------------------------ operations

    private static void WriteOperations(StringBuilder sb, ExtractedProject project)
    {
        var actions = project.Controllers.Sum(c => c.Actions.Count);

        sb.AppendLine("<section id=\"operations\">");
        sb.AppendLine($"  <h2>Business operations <span class=\"card__meta\">{actions}</span></h2>");
        sb.AppendLine("  <p class=\"lede\">What a user can make this application do, and the code that runs when they do it.</p>");

        if (project.Controllers.Count == 0)
        {
            sb.AppendLine("  <p class=\"empty\">This application defines no controllers.</p>");
            sb.AppendLine("</section>");
            return;
        }

        foreach (var controller in project.Controllers.OrderBy(c => c.ClassName, StringComparer.Ordinal))
        {
            var haystack = Haystack(controller.ClassName, controller.TargetObjectType, controller.BusinessLogicSummary,
                string.Join(" ", controller.Actions.Select(a => a.ActionId + " " + a.Caption)));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(controller.ClassName)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">on {E(Scope(controller))}</span>");

            // Only present when a ground-truth catalog identified the base as a DevExpress type:
            // this controller changes shipped behaviour rather than adding something beside it.
            if (!string.IsNullOrWhiteSpace(controller.FrameworkBaseType))
                sb.AppendLine($"      <span class=\"pill pill--fw\">extends built-in {E(controller.FrameworkBaseType)}</span>");

            sb.AppendLine("    </div>");

            if (!string.IsNullOrWhiteSpace(controller.BusinessLogicSummary))
                sb.AppendLine($"    <p class=\"card__desc\">{E(controller.BusinessLogicSummary)}</p>");

            if (controller.Actions.Count == 0)
            {
                sb.AppendLine("    <p class=\"card__desc empty\">No actions — it customizes behavior through view events or defaults.</p>");
            }

            foreach (var action in controller.Actions)
            {
                sb.AppendLine("    <table><tbody>");
                sb.AppendLine($"      <tr><th>Action</th><td class=\"mono\">{E(action.ActionId)}</td></tr>");
                if (!string.IsNullOrWhiteSpace(action.Caption))
                    sb.AppendLine($"      <tr><th>Button</th><td>“{E(action.Caption)}”</td></tr>");
                if (!string.IsNullOrWhiteSpace(action.ConfirmationMessage))
                    sb.AppendLine($"      <tr><th>Confirms</th><td>“{E(action.ConfirmationMessage)}”</td></tr>");
                if (!string.IsNullOrWhiteSpace(action.EnabledCriteria))
                    sb.AppendLine($"      <tr><th>Enabled when</th><td><code class=\"crit\">{E(action.EnabledCriteria)}</code></td></tr>");
                sb.AppendLine("    </tbody></table>");

                if (!string.IsNullOrWhiteSpace(action.BusinessLogicSummary))
                    sb.AppendLine($"    <p class=\"card__desc\">{E(action.BusinessLogicSummary)}</p>");

                if (!string.IsNullOrWhiteSpace(action.ExecuteMethodBody))
                {
                    sb.AppendLine($"    <details><summary>What it runs</summary><pre><code>{E(Snippet(action.ExecuteMethodBody))}</code></pre></details>");
                }
            }

            var helpers = controller.Methods.Where(m => !string.IsNullOrWhiteSpace(m.Body)).ToList();
            if (helpers.Count > 0)
            {
                sb.AppendLine($"    <details><summary>Helper methods ({helpers.Count})</summary>");
                foreach (var method in helpers)
                {
                    sb.AppendLine($"      <pre><code>{E(method.ReturnType)} {E(method.Name)}({E(string.Join(", ", method.Parameters))})\n{E(Snippet(method.Body))}</code></pre>");
                }
                sb.AppendLine("    </details>");
            }

            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    /// <summary>
    /// Says in a few words where a controller runs, from all four of its conditions.
    /// </summary>
    /// <remarks>
    /// This read <c>TargetObjectType</c> alone, so a <c>ViewController&lt;DetailView&gt;</c> with no
    /// object type was labelled "on any view" — while the screens section, from the same data,
    /// correctly had it on detail views only.
    /// </remarks>
    private static string Scope(ExtractedController controller)
    {
        var targeting = controller.Targeting;

        if (controller.IsWindowController)
            return "windows, not views";

        if (targeting.IsUndetermined)
            return "views that could not be determined";

        var where = (targeting.TargetObjectType, targeting.TypeOfView) switch
        {
            (null, null) => "any view",
            (null, var view) => $"any {view}",
            (var type, null) => type,
            var (type, view) => $"{type} — {view} only",
        };

        return targeting.Nesting is { } nesting ? $"{where}, {nesting.ToLowerInvariant()} only" : where;
    }

    // --------------------------------------------------------------- screens

    /// <summary>
    /// Writes the screen inventory: every view, and the logic loaded onto it.
    /// </summary>
    /// <remarks>
    /// The section nobody can produce by reading the repository. XAF generates a list, detail and
    /// lookup view for every business class and a list view for every collection, so the screens
    /// exist in no file; and a controller's activation is four conditions the framework ands
    /// together, evaluated at run time against a view nobody wrote down.
    /// </remarks>
    private static void WriteScreens(StringBuilder sb, ExtractedProject project)
    {
        if (project.Views.Count == 0)
            return;

        var generated = project.Views.Count(v => v.Origin == ViewOrigin.Generated);
        var everywhere = project.Controllers.Count(c => c.Targeting.IsUnrestricted);

        sb.AppendLine("<section id=\"screens\">");
        sb.AppendLine($"  <h2>Screens <span class=\"card__meta\">{project.Views.Count}</span></h2>");
        sb.AppendLine("  <p class=\"lede\">Every view this application has, and what XAF loads onto each one. " +
                      $"{generated} of them exist in no file — the framework generates them from the business classes at startup.</p>");

        if (everywhere > 0)
        {
            sb.AppendLine($"  <p class=\"note\">{everywhere} controller{(everywhere == 1 ? "" : "s")} " +
                          $"restrict{(everywhere == 1 ? "s" : "")} nothing, so {(everywhere == 1 ? "it runs" : "they run")} " +
                          "on every screen below.</p>");
        }

        foreach (var group in project.Views
                     .GroupBy(v => v.ObjectType ?? "Other")
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            sb.AppendLine($"  <article class=\"card\" id=\"screens-{E(group.Key)}\" " +
                          $"data-search=\"{Haystack(group.Key, string.Join(" ", group.Select(v => v.Id)))}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(group.Key)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{group.Count()} views</span>");
            sb.AppendLine("    </div>");

            foreach (var view in group.OrderBy(v => v.Id, StringComparer.Ordinal))
            {
                sb.AppendLine("    <table><tbody>");
                sb.Append($"      <tr><th class=\"mono id\">{E(view.Id)}</th><td>");
                sb.Append(E(ScreenKind(view)));

                if (view.InNavigation)
                    sb.Append(" <span class=\"pill\">in navigation</span>");

                if (view.Origin != ViewOrigin.Generated)
                {
                    sb.Append(view.Origin == ViewOrigin.Customized
                        ? " <span class=\"pill pill--fw\">customized in the Model Editor</span>"
                        : " <span class=\"pill pill--fw\">defined in the Model Editor</span>");
                }

                sb.AppendLine("</td></tr>");

                if (view.OwnerProperty is { } ownerProperty)
                {
                    sb.AppendLine("      <tr><th>Shown by</th><td class=\"mono\">" +
                                  $"<a href=\"#entity-{E(view.OwnerEntity)}\">{E(view.OwnerEntity)}</a>.{E(ownerProperty)}</td></tr>");
                }

                var mine = view.Activates.Where(a => !a.Framework).ToList();
                var framework = view.Activates.Where(a => a.Framework).ToList();

                if (mine.Count == 0)
                {
                    sb.AppendLine("      <tr><th>Runs here</th><td class=\"t\">None of this application's controllers.</td></tr>");
                }

                foreach (var activation in mine)
                {
                    sb.AppendLine($"      <tr><th>Runs here</th><td class=\"mono\">{E(activation.Controller)}</td></tr>");
                    sb.Append("      <tr><th></th><td class=\"t\">");
                    sb.Append(activation.Reasons.Count == 0
                        ? "restricts nothing, so it runs on every view"
                        : E(string.Join(", and ", activation.Reasons.Select(ActivationReasonText.English))));

                    if (activation.Replaces.Count > 0)
                        sb.Append($" — <strong>replaces {E(string.Join(", ", activation.Replaces))}</strong>");

                    if (activation.Actions.Count > 0)
                        sb.Append($" — {E(string.Join(", ", activation.Actions))}");

                    sb.AppendLine("</td></tr>");
                }

                sb.AppendLine("    </tbody></table>");

                // Folded away deliberately. It is inherited behaviour, there is a great deal of it,
                // and giving it the same weight as the team's own would bury the line worth reading.
                if (framework.Count > 0)
                {
                    sb.AppendLine($"    <details><summary>{framework.Count} more from XAF itself</summary>");
                    sb.AppendLine("      <table><tbody>");

                    foreach (var activation in framework.OrderBy(a => a.Controller, StringComparer.Ordinal))
                    {
                        sb.Append($"        <tr><th class=\"mono id\">{E(activation.Controller)}</th><td class=\"t\">");
                        sb.Append(E(activation.Summary ?? activation.SourceProject ?? ""));
                        sb.AppendLine("</td></tr>");
                    }

                    sb.AppendLine("      </tbody></table>");
                    sb.AppendLine("    </details>");
                }
            }

            sb.AppendLine("  </article>");
        }

        var undetermined = Analyzers.ViewActivationResolver.Undetermined(project.Controllers).ToList();

        if (undetermined.Count > 0)
        {
            sb.AppendLine("  <article class=\"card\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine("      <span class=\"card__name card__name--prose\">Where these run could not be established</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <p class=\"card__desc\">These appear against no screen above, because something " +
                          "about where they activate cannot be read from the source. They are restricted all " +
                          "the same — listing them on every screen would be the bigger error.</p>");
            sb.AppendLine("    <table><tbody>");

            foreach (var controller in undetermined)
            {
                sb.AppendLine($"      <tr><th class=\"mono id\">{E(controller.ClassName)}</th>" +
                              $"<td class=\"t\">{E(Analyzers.ViewActivationResolver.UndeterminedReason(controller))}</td></tr>");
            }

            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("  </article>");
        }

        if (project.FrameworkAlwaysActive.Count > 0)
        {
            sb.AppendLine("  <article class=\"card\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name card__name--prose\">XAF controllers on every screen</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{project.FrameworkAlwaysActive.Count}</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine("    <p class=\"card__desc\">These restrict nothing, so they load onto all " +
                          $"{project.Views.Count} screens above. Listed once rather than {project.Views.Count} times.</p>");
            sb.AppendLine($"    <p class=\"card__desc mono\">{E(string.Join(", ", project.FrameworkAlwaysActive))}</p>");
            sb.AppendLine("  </article>");
        }
        else if (project.CatalogVersion is null)
        {
            sb.AppendLine("  <p class=\"note\">XAF's own controllers run on these screens too, and naming them " +
                          "needs the ground-truth catalog. Run <code>xaflogic catalog build</code> on a machine " +
                          "with a DevExpress licence and this section will say which.</p>");
        }

        sb.AppendLine("  <p class=\"note\">A controller listed here can still switch itself off at run time " +
                      "through <code>Active[\"reason\"]</code>, which depends on the data and the user. " +
                      "This is what XAF loads onto a screen, not what will necessarily do something.</p>");
        sb.AppendLine("</section>");
    }

    /// <summary>One phrase naming what kind of view this is and where it appears.</summary>
    private static string ScreenKind(ExtractedView view)
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

        return $"{kind} view, {where}";
    }

    // ----------------------------------------------------------------- rules

    private static void WriteRules(StringBuilder sb, ExtractedProject project)
    {
        var withRules = project.Entities
            .Where(e => e.ValidationRules.Count > 0 || e.AppearanceRules.Count > 0
                     || e.Properties.Any(p => !string.IsNullOrWhiteSpace(p.PersistentAlias)))
            .OrderBy(e => e.ClassName, StringComparer.Ordinal)
            .ToList();

        sb.AppendLine("<section id=\"rules\">");
        sb.AppendLine("  <h2>What the application enforces</h2>");
        sb.AppendLine("  <p class=\"lede\">Validation the user will hit, behavior that changes with the data, and figures the database computes.</p>");

        if (withRules.Count == 0)
        {
            sb.AppendLine("  <p class=\"empty\">No validation, appearance rules or calculated properties are declared.</p>");
            sb.AppendLine("</section>");
            return;
        }

        foreach (var entity in withRules)
        {
            var calculated = entity.Properties.Where(p => !string.IsNullOrWhiteSpace(p.PersistentAlias)).ToList();
            var haystack = Haystack(entity.ClassName,
                string.Join(" ", entity.ValidationRules.Select(r => r.RuleType + " " + r.MessageTemplate)),
                string.Join(" ", entity.AppearanceRules.Select(r => r.Id + " " + r.Criteria)));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\">");
            sb.AppendLine($"    <div class=\"card__head\"><span class=\"card__name\">{E(entity.ClassName)}</span></div>");

            if (entity.ValidationRules.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Validation</th><th>On</th><th>The user is told</th></tr></thead><tbody>");
                foreach (var rule in entity.ValidationRules)
                {
                    sb.Append($"      <tr><td class=\"mono t\">{E(rule.RuleType)}</td><td class=\"mono\">{E(rule.TargetProperty ?? "—")}</td><td>");
                    sb.Append(string.IsNullOrWhiteSpace(rule.MessageTemplate)
                        ? "<span class=\"empty\">default message</span>"
                        : $"“{E(rule.MessageTemplate)}”");
                    if (!string.IsNullOrWhiteSpace(rule.TargetCriteria))
                        sb.Append($" <span class=\"t\">when <code class=\"crit\">{E(rule.TargetCriteria)}</code></span>");
                    sb.AppendLine("</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            if (entity.AppearanceRules.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Appearance</th><th>When</th><th>Effect</th></tr></thead><tbody>");
                foreach (var rule in entity.AppearanceRules)
                {
                    var effects = new List<string>();
                    if (!string.IsNullOrWhiteSpace(rule.Visibility)) effects.Add($"visibility {E(rule.Visibility)}");
                    if (!string.IsNullOrWhiteSpace(rule.Enabled)) effects.Add($"enabled {E(rule.Enabled)}");
                    if (!string.IsNullOrWhiteSpace(rule.BackColor)) effects.Add($"background {E(rule.BackColor)}");
                    if (!string.IsNullOrWhiteSpace(rule.FontColor)) effects.Add($"text {E(rule.FontColor)}");

                    sb.AppendLine($"      <tr><td class=\"mono\">{E(rule.Id)}</td>" +
                                  $"<td><code class=\"crit\">{E(rule.Criteria ?? "always")}</code></td>" +
                                  $"<td class=\"t\">{string.Join(", ", effects)}</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            if (calculated.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Calculated</th><th>Expression</th></tr></thead><tbody>");
                foreach (var property in calculated)
                    sb.AppendLine($"      <tr><td class=\"mono\">{E(property.Name)}</td><td><code class=\"crit\">{E(property.PersistentAlias)}</code></td></tr>");
                sb.AppendLine("    </tbody></table>");
            }

            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    // -------------------------------------------------------------- criteria

    private static void WriteCriteria(StringBuilder sb, ExtractedProject project)
    {
        var conventions = CodebaseConventions.Infer(project);

        sb.AppendLine("<section id=\"criteria\">");
        sb.AppendLine("  <h2>Criteria expressions</h2>");
        // "Every expression" while the collector deduplicates by expression and drew from four of
        // the six places they occur. It now reads them all, and says what it did with duplicates
        // rather than leaving a reader to assume there were none.
        sb.AppendLine("  <p class=\"lede\">XAF filters, validates and styles with criteria strings — neither SQL nor C#. Every <em>distinct</em> expression in this application, gathered from validation and appearance rules, lookup filters, action availability and the Model Editor. An expression used twice appears once.</p>");

        if (conventions.CriteriaExamples.Count == 0)
        {
            sb.AppendLine("  <p class=\"empty\">This application declares no criteria expressions.</p>");
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <article class=\"card\" data-search=\"criteria expressions filter\">");
        sb.AppendLine("    <table><thead><tr><th>Expression</th><th>Where</th></tr></thead><tbody>");
        foreach (var example in conventions.CriteriaExamples)
            sb.AppendLine($"      <tr><td><code class=\"crit\">{E(example.Expression)}</code></td><td class=\"t\">{E(example.Context)}</td></tr>");
        sb.AppendLine("    </tbody></table>");
        sb.AppendLine("  </article>");
        sb.AppendLine("</section>");
    }

    // ---------------------------------------------------------- model editor

    private static void WriteModelEditor(StringBuilder sb, ExtractedProject project)
    {
        if (project.ModelEditorInfo is not { } model)
            return;

        sb.AppendLine("<section id=\"model\">");
        sb.AppendLine("  <h2>Model Editor</h2>");
        sb.AppendLine("  <p class=\"lede\"><strong>This behavior exists only in XML.</strong> None of it appears in the C#, so reading the business classes alone gives a picture of the screens that is wrong.</p>");

        if (model.BOModelClasses.Count == 0 && model.Views.Count == 0)
        {
            sb.AppendLine("  <p class=\"empty\">No Model Editor customizations — the UI follows from the business classes and XAF defaults.</p>");
            sb.AppendLine("</section>");
            return;
        }

        sb.AppendLine("  <article class=\"card\" data-search=\"model editor xafml views captions\">");

        if (!string.IsNullOrWhiteSpace(model.ApplicationTitle))
            sb.AppendLine($"    <p class=\"card__desc\">Application title: “{E(model.ApplicationTitle)}”</p>");

        if (model.BOModelClasses.Count > 0)
        {
            sb.AppendLine("    <table><thead><tr><th>Class</th><th>Caption</th><th></th></tr></thead><tbody>");
            foreach (var boClass in model.BOModelClasses)
            {
                sb.AppendLine($"      <tr><td class=\"mono\">{E(boClass.ClassName)}</td>" +
                              $"<td>{E(boClass.Caption ?? "—")}</td>" +
                              $"<td>{(boClass.IsCloneable ? "<span class=\"pill\">cloneable</span>" : "")}</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
        }

        if (model.Views.Count > 0)
        {
            sb.AppendLine("    <table><thead><tr><th>View</th><th>Type</th></tr></thead><tbody>");
            foreach (var view in model.Views)
                sb.AppendLine($"      <tr><td class=\"mono\">{E(view.Id)}</td><td class=\"t\">{E(view.ViewType.ToString())}</td></tr>");
            sb.AppendLine("    </tbody></table>");
        }

        if (model.SourceFiles.Count > 0)
            sb.AppendLine($"    <p class=\"card__desc t\">Merged from {model.SourceFiles.Count} .xafml file{(model.SourceFiles.Count == 1 ? "" : "s")}.</p>");

        sb.AppendLine("  </article>");
        sb.AppendLine("</section>");
    }

    // --------------------------------------------------------- custom editors

    private static void WriteEditors(StringBuilder sb, ExtractedProject project)
    {
        var customized = project.Controllers
            .Where(c => c.CustomizedEditors.Count > 0)
            .OrderBy(c => c.ClassName, StringComparer.Ordinal)
            .ToList();

        if (project.Editors.Count == 0 && customized.Count == 0)
            return;

        sb.AppendLine("<section id=\"editors\">");
        // The heading used to assert that screens deviate. Some of these editors are registered and
        // requested by nothing, so the section contradicted itself two rows down.
        sb.AppendLine("  <h2>Editors this application defines</h2>");
        sb.AppendLine("  <p class=\"lede\"><strong>A property with a custom editor does not render the way its type implies</strong>, and the business class says nothing about it. These usually live in a platform project beside the module, so nobody reading the business objects meets them. Each row says whether anything currently asks for it.</p>");

        foreach (var editor in project.Editors)
        {
            var haystack = Haystack(editor.ClassName, editor.TargetType, editor.Alias, editor.Description);

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(editor.ClassName)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{E(Describe(editor.Kind))}");
            if (!string.IsNullOrWhiteSpace(editor.SourceProject))
                sb.Append($" · in {E(editor.SourceProject)}");
            sb.AppendLine("</span>");

            // Blast radius, not style. Both of these change screens nobody edited, which is the
            // one thing a reader needs to know before touching the editor.
            if (editor.IsDefault)
                sb.AppendLine("      <span class=\"pill pill--req\">replaces the default everywhere</span>");
            if (editor.TargetType is "object" or "Object")
                sb.AppendLine("      <span class=\"pill pill--req\">applies to every property</span>");

            sb.AppendLine("    </div>");

            if (!string.IsNullOrWhiteSpace(editor.Description))
                sb.AppendLine($"    <p class=\"card__desc\">{E(editor.Description)}</p>");

            sb.AppendLine("    <table><tbody>");
            if (!string.IsNullOrWhiteSpace(editor.TargetType))
                sb.AppendLine($"      <tr><th>Renders</th><td class=\"mono\">{E(editor.TargetType)}</td></tr>");
            if (!string.IsNullOrWhiteSpace(editor.Alias))
                sb.AppendLine($"      <tr><th>Alias</th><td class=\"mono\">{E(editor.Alias)}</td></tr>");
            if (!string.IsNullOrWhiteSpace(editor.BaseType))
                sb.AppendLine($"      <tr><th>Based on</th><td class=\"mono t\">{E(editor.BaseType)}</td></tr>");
            if (editor.UsedBy.Count > 0)
            {
                var links = editor.UsedBy.Select(e => $"<a href=\"#entity-{E(e)}\">{E(e)}</a>");
                sb.AppendLine($"      <tr><th>Used by</th><td>{string.Join(", ", links)}</td></tr>");
            }
            else if (!editor.IsDefault && !string.IsNullOrWhiteSpace(editor.Alias))
            {
                // Registered but not applied. Saying so is more useful than an empty row, and far
                // more useful than guessing at every property of the target type.
                sb.AppendLine("      <tr><th>Used by</th><td class=\"t\">Nothing requests it by alias. " +
                              "It is selectable in the Model Editor, which may assign it to a view.</td></tr>");
            }
            if (editor.ClientAssets.Count > 0)
            {
                sb.AppendLine($"      <tr><th>Needs</th><td class=\"mono\">{E(string.Join(", ", editor.ClientAssets))}" +
                              "<div class=\"t\">Client-side files it cannot work without — behavior in neither C# nor XML.</div></td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("  </article>");
        }

        if (customized.Count > 0)
        {
            sb.AppendLine("  <article class=\"card\" data-search=\"customized built-in editors controllers\">");
            sb.AppendLine("    <div class=\"card__head\"><span class=\"card__name card__name--prose\">Built-in editors reconfigured at run time</span></div>");
            sb.AppendLine("    <p class=\"card__desc\">No custom editor class exists for these. A controller reaches into a built-in editor's component model, so nothing on the entity or in the Model Editor mentions it.</p>");
            sb.AppendLine("    <table><thead><tr><th>Controller</th><th>Changes</th></tr></thead><tbody>");
            foreach (var controller in customized)
            {
                sb.AppendLine($"      <tr><td class=\"mono\">{E(controller.ClassName)}</td>" +
                              $"<td class=\"mono t\">{E(string.Join(", ", controller.CustomizedEditors))}</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    private static string Describe(EditorKind kind) => kind switch
    {
        EditorKind.PropertyEditor => "property editor",
        EditorKind.ListEditor => "list editor",
        EditorKind.ViewItem => "view item",
        _ => "editor",
    };

    // ------------------------------------------------------------- seed data

    private static void WriteSeedData(StringBuilder sb, ExtractedProject project)
    {
        if (project.SeedData.Count == 0)
            return;

        sb.AppendLine("<section id=\"seed\">");
        sb.AppendLine("  <h2>What exists on a fresh database</h2>");
        sb.AppendLine("  <p class=\"lede\">Records the module updater creates on first run. Often the answer to “where did this row come from?”.</p>");

        foreach (var seed in project.SeedData)
        {
            sb.AppendLine($"  <article class=\"card\" data-search=\"{Haystack(seed.MethodName, seed.EntityType, seed.Description)}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(seed.MethodName)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">creates {E(seed.EntityType)} · {seed.Records.Count} record{(seed.Records.Count == 1 ? "" : "s")}</span>");
            sb.AppendLine("    </div>");

            var columns = seed.Records
                .SelectMany(r => r.PropertyValues.Keys)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (columns.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr>");
                foreach (var column in columns) sb.Append($"<th>{E(column)}</th>");
                sb.AppendLine("</tr></thead><tbody>");
                foreach (var record in seed.Records)
                {
                    sb.Append("      <tr>");
                    foreach (var column in columns)
                        sb.Append($"<td class=\"mono\">{E(record.PropertyValues.GetValueOrDefault(column, "—"))}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    // ------------------------------------------------------------ migrations

    private static void WriteMigrations(StringBuilder sb, ExtractedProject project)
    {
        if (project.Migrations.Count == 0)
            return;

        sb.AppendLine("<section id=\"migrations\">");
        sb.AppendLine("  <h2>What happened to the data</h2>");
        // What the source proves is the guard, not the history: a version-gated block runs at most
        // once for any given database. Whether a particular database ever passed through it is not
        // in this repository, and stating it as fact was turning intent into an event.
        sb.AppendLine("  <p class=\"lede\">Each of these runs <strong>at most once</strong> for any database, when it is upgraded past the version named — and never again after that. Reading the current code cannot recover what they did, which makes this the answer to “why does this column contain that?”.</p>");

        foreach (var migration in project.Migrations
                     .OrderBy(m => m.TargetVersion, StringComparer.Ordinal))
        {
            var haystack = Haystack(migration.TargetVersion, migration.Description,
                string.Join(" ", migration.CallsMethods));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">upgrading to {E(migration.TargetVersion ?? "an unknown version")}</span>");
            sb.AppendLine($"      <span class=\"pill\">{E(Describe(migration.Phase))}</span>");
            // "from X" includes X; the guard is `CurrentDBVersion > X`, which excludes it -- and X
            // is usually 0.0.0.0, the version a database has before it has ever been updated. The
            // one value the range definitely does not cover was named as its lower bound.
            if (!string.IsNullOrWhiteSpace(migration.MinimumVersion))
                sb.AppendLine($"      <span class=\"card__meta\">existing databases only, above {E(migration.MinimumVersion)}</span>");
            sb.AppendLine("    </div>");

            // The comment is the only record of *why*, which is the question a reader has.
            if (!string.IsNullOrWhiteSpace(migration.Description))
                sb.AppendLine($"    <p class=\"card__desc\">{E(migration.Description)}</p>");

            sb.AppendLine("    <table><tbody>");
            sb.AppendLine($"      <tr><th>Runs when</th><td class=\"mono\"><code class=\"crit\">{E(migration.Condition)}</code></td></tr>");
            if (migration.CallsMethods.Count > 0)
                sb.AppendLine($"      <tr><th>Calls</th><td class=\"mono\">{E(string.Join(", ", migration.CallsMethods))}</td></tr>");
            sb.AppendLine("    </tbody></table>");

            if (!string.IsNullOrWhiteSpace(migration.Code))
                sb.AppendLine($"    <details><summary>What it did</summary><pre><code>{E(Snippet(migration.Code))}</code></pre></details>");

            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    private static string Describe(MigrationPhase phase) => phase switch
    {
        // Stated as a consequence, not as a method name: a reader needs to know which columns
        // existed when the block ran, not which override it sat in.
        MigrationPhase.BeforeSchemaUpdate => "before the schema changed — new columns did not exist yet",
        MigrationPhase.AfterSchemaUpdate => "after the schema changed — anything dropped was already gone",
        _ => "phase unknown",
    };

    private void WriteFooter(StringBuilder sb, ExtractedProject project)
    {
        sb.AppendLine("<footer><div class=\"wrap\">");
        sb.Append($"  <p>Generated from source by <a href=\"https://github.com/peopleworks/XAFLogicExplainer\">XAF Logic Explainer</a> {E(_toolVersion)}");
        sb.Append($" on {E(project.ExtractedAt)}");
        if (!string.IsNullOrWhiteSpace(project.CatalogVersion))
            sb.Append($", checked against the DevExpress {E(project.CatalogVersion)} framework catalog");
        sb.AppendLine(".</p>");
        sb.AppendLine("  <p>Nothing here was written by hand, and nothing was inferred by a language model: every statement is read from the code. Regenerate with <code>xaflogic explain</code>.</p>");
        sb.AppendLine("</div></footer>");
    }

    // --------------------------------------------------------------- helpers

    private static bool IsEfCore(string? orm) =>
        orm is not null && orm.Contains("EF", StringComparison.OrdinalIgnoreCase);

    private static string Describe(RelationshipType type) => type switch
    {
        RelationshipType.OneToMany => "has many",
        RelationshipType.ManyToOne => "belongs to",
        RelationshipType.ManyToMany => "many to many",
        _ => "related",
    };

    /// <summary>Builds the lower-cased blob the client-side search matches against.</summary>
    private static string Haystack(params string?[] parts) =>
        E(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant());

    private static string Cap(string code) =>
        code.Length <= MaxCodeLength
            ? code
            : code[..MaxCodeLength] + "\n\n// … truncated; read the source file for the rest.";

    /// <summary>
    /// Removes the indentation the snippet had in its source file.
    /// </summary>
    /// <remarks>
    /// Roslyn hands back the node's text, which starts where the node starts — so the first line
    /// carries no indentation while every line under it still carries the file's. Rendered as-is,
    /// a two-line method body opens flush left and then jumps eight columns.
    /// <para>
    /// The first line is therefore excluded from the measurement when it is already flush left:
    /// including it would pin the common indent at zero and dedent nothing.
    /// </para>
    /// </remarks>
    private static string Dedent(string code)
    {
        var lines = code.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 2)
        {
            return code.Trim();
        }

        var measured = lines.Skip(lines[0].Length > 0 && !char.IsWhiteSpace(lines[0][0]) ? 1 : 0)
                            .Where(line => line.Trim().Length > 0)
                            .ToList();
        if (measured.Count == 0)
        {
            return code.Trim();
        }

        var common = measured.Min(line => line.Length - line.TrimStart().Length);

        return string.Join('\n', lines.Select(line =>
            line.Length >= common && line[..common].Trim().Length == 0
                ? line[common..].TrimEnd()
                : line.TrimEnd())).Trim('\n');
    }

    /// <summary>
    /// Prepares a source snippet for display: source indentation removed, length capped.
    /// </summary>
    private static string Snippet(string code) => Cap(Dedent(code));

    /// <summary>
    /// Escapes text for HTML.
    /// </summary>
    /// <remarks>
    /// Applied to every interpolated value without exception. The input is somebody's source code:
    /// generic types, comparisons and string literals are full of angle brackets and ampersands,
    /// and one unescaped method body would silently swallow the rest of the page.
    /// </remarks>
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
