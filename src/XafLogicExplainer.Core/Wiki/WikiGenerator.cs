using System.Net;
using System.Text;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// Writes one self-contained page covering every XAF application a developer has.
/// </summary>
/// <remarks>
/// A single-application explainer answers "how does this work". A wiki answers a question that
/// cannot be asked of one application at a time: <em>have I built this before?</em> Someone who has
/// delivered XAF applications to a dozen clients over ten years cannot hold the answer in their
/// head, and the answer is worth money — it is the difference between reusing a class and writing
/// it a fourth time.
/// <para>
/// One file, no dependencies, opens by double-click. Everything in it was read from source; the
/// page has no place to put a sentence somebody typed about the corpus, which is the point.
/// </para>
/// </remarks>
public sealed class WikiGenerator
{
    private readonly string _toolVersion;

    /// <summary>
    /// Creates a generator that stamps the page with the tool version that produced it.
    /// </summary>
    public WikiGenerator(string toolVersion = GeneratorVersion.Unknown) => _toolVersion = toolVersion;

    /// <summary>
    /// Writes the whole wiki.
    /// </summary>
    /// <param name="corpus">The applications and what they share.</param>
    /// <param name="title">A name for the collection. Defaults to a neutral one.</param>
    public string Generate(WikiCorpus corpus, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var name = string.IsNullOrWhiteSpace(title) ? "Your XAF applications" : title.Trim();
        var sb = new StringBuilder();

        sb.AppendLine("<!doctype html>");
        sb.AppendLine("<html lang=\"en\">");
        WriteHead(sb, name);
        sb.AppendLine("<body>");

        WriteHeader(sb, corpus, name);
        WriteNav(sb, corpus);

        sb.AppendLine("<main class=\"wrap\">");
        WritePictures(sb, corpus);
        WriteShared(sb, corpus);
        WriteApplications(sb, corpus);

        foreach (var app in corpus.Applications)
            WriteApplication(sb, app);

        WriteLimits(sb, corpus);
        sb.AppendLine("</main>");

        WriteFooter(sb, corpus);

        sb.AppendLine($"<script>{WikiStyles.Js}</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    // ------------------------------------------------------------------ head

    private static void WriteHead(StringBuilder sb, string name)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{E(name)} — read together</title>");
        sb.AppendLine("<meta name=\"generator\" content=\"XAF Logic Explainer\">");
        // Inline, so a page opened from disk or from a share does not ask a server for an icon it
        // has no way to provide.
        sb.AppendLine("<link rel=\"icon\" href=\"data:image/svg+xml,"
                    + "%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E"
                    + "%3Crect width='32' height='32' rx='7' fill='%230a0d12'/%3E"
                    + "%3Cpath d='M7 9h7v14H7zM18 9h7v14h-7z' fill='none' stroke='%232dd4bf' stroke-width='2.2'/%3E"
                    + "%3Cpath d='M14 16h4' stroke='%23ff8a3d' stroke-width='2.2' stroke-linecap='round'/%3E"
                    + "%3C/svg%3E\">");
        sb.AppendLine($"<style>{WikiStyles.Css}</style>");
        sb.AppendLine("</head>");
    }

    private static void WriteHeader(StringBuilder sb, WikiCorpus corpus, string name)
    {
        var entities = corpus.Applications.Sum(a => a.Project.Entities.Count);
        var controllers = corpus.Applications.Sum(a => a.Project.Controllers.Count);
        var actions = corpus.Applications.Sum(a => a.ActionCount);
        var reports = corpus.Applications.Sum(a => a.Project.Reports.Count);

        sb.AppendLine("<header><div class=\"wrap\">");
        sb.AppendLine("  <div class=\"head\">");
        sb.AppendLine("    <div class=\"head__id\">");
        sb.AppendLine($"      <h1>{E(name)}</h1>");
        sb.AppendLine($"      <p class=\"head__sub\">{Count(corpus.Applications.Count, "application")} read from source and compared with each other. "
                    + "Anything <span class=\"shared-ink\">in this colour</span> appears in more than one of them.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"head__tools\">");
        sb.AppendLine("      <input id=\"q\" type=\"search\" placeholder=\"Search every application…\" aria-label=\"Search\">");
        sb.AppendLine("      <button class=\"iconbtn\" id=\"theme\" type=\"button\" aria-label=\"Switch light or dark\">◐</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div id=\"count\"></div>");

        sb.AppendLine("  <div class=\"stats\">");
        Stat(sb, corpus.Applications.Count, "applications");
        Stat(sb, entities, "entities");
        Stat(sb, controllers, "controllers");
        Stat(sb, actions, "actions");
        if (reports > 0) Stat(sb, reports, "reports");
        Stat(sb, corpus.ModelledTwiceCount, "classes modelled twice");
        Stat(sb, corpus.RecurringBaseTypes.Count, "base classes reused");
        sb.AppendLine("  </div>");

        if (corpus.Applications.Count > 1)
        {
            sb.AppendLine("  <div class=\"filters\">");
            sb.AppendLine("    <span class=\"filters__label\">Only what touches:</span>");
            foreach (var app in corpus.Applications)
                sb.AppendLine($"    <button class=\"chip\" type=\"button\" data-slug=\"{E(app.Slug)}\">{E(app.Name)}</button>");
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("</div></header>");
    }

    private static void Stat(StringBuilder sb, int value, string label) =>
        sb.AppendLine($"    <div class=\"stat\"><b>{value}</b><span>{label}</span></div>");

    private static void WriteNav(StringBuilder sb, WikiCorpus corpus)
    {
        sb.AppendLine("<nav><div class=\"wrap\"><ul>");
        if (corpus.Applications.Count > 1 && corpus.ModelledTwiceCount > 0)
            sb.AppendLine("  <li><a href=\"#map\">The map</a></li>");

        sb.AppendLine("  <li><a href=\"#shared\">In common</a></li>");
        sb.AppendLine("  <li><a href=\"#applications\">The applications</a></li>");

        foreach (var app in corpus.Applications)
            sb.AppendLine($"  <li><a href=\"#app-{E(app.Slug)}\">{E(app.Name)}</a></li>");

        sb.AppendLine("  <li><a href=\"#limits\">What this cannot tell you</a></li>");
        sb.AppendLine("</ul></div></nav>");
    }

    // ------------------------------------------------------------- pictures

    private static void WritePictures(StringBuilder sb, WikiCorpus corpus)
    {
        var map = CorpusGraph.Map(corpus);
        var overlap = CorpusGraph.Overlap(corpus);
        var versions = CorpusGraph.Versions(corpus);

        if (map.IsEmpty && overlap.IsEmpty && versions.IsEmpty)
            return;

        sb.AppendLine("<section id=\"map\">");
        sb.AppendLine("  <h2>Where your applications meet</h2>");

        WriteConstellation(sb, map);
        WriteOverlap(sb, overlap);
        WriteVersions(sb, versions);

        sb.AppendLine("</section>");
    }

    /// <summary>
    /// The applications on a ring, and every shared class between the ones that model it.
    /// </summary>
    /// <remarks>
    /// The layout carries the argument, so the picture reads before the caption does: the middle is
    /// what everybody models and the rim is what belongs to one client. Nothing is placed by hand
    /// and nothing is random, so two runs of the same corpus draw the same picture.
    /// </remarks>
    private static void WriteConstellation(StringBuilder sb, CorpusMap map)
    {
        if (map.IsEmpty)
            return;

        sb.AppendLine("  <p class=\"lede\">Each class modelled in more than one application, placed between "
                    + "the applications that model it. The closer to the middle, the more of them share it — "
                    + "a class every application has belongs to no one direction, so it falls to the centre. "
                    + "Hover to isolate; click a class to jump to its comparison, or an application to jump to it.</p>");

        sb.AppendLine($"  <div class=\"cmap\" id=\"cmap\">");
        sb.AppendLine($"  <svg viewBox=\"0 0 {N(map.Width)} {N(map.Height)}\" role=\"img\" "
                    + "aria-label=\"Classes modelled in more than one application, placed between them\">");

        foreach (var link in map.Links)
        {
            var application = map.Applications.First(a => a.Slug == link.Slug);
            var shared = map.Classes.First(c => c.ClassName == link.ClassName);

            sb.AppendLine($"    <path class=\"link\" data-slug=\"{E(link.Slug)}\" data-class=\"{E(link.ClassName)}\" "
                        + $"d=\"{Bow(application.X, application.Y, shared.X, shared.Y, map)}\"/>");
        }

        foreach (var shared in map.Classes)
        {
            // Named on the picture only when several applications model it. Labelling all of them
            // turns the middle into a word cloud; the rest answer on hover and in the tooltip.
            var named = shared.Slugs.Count >= 3 ? " is-named" : string.Empty;

            sb.AppendLine($"    <g class=\"cnode{named}\" data-class=\"{E(shared.ClassName)}\" tabindex=\"0\" role=\"button\" "
                        + $"aria-label=\"{E(shared.ClassName)}, modelled in {Count(shared.Slugs.Count, "application")}\">");
            sb.AppendLine($"      <circle cx=\"{N(shared.X)}\" cy=\"{N(shared.Y)}\" r=\"{N(shared.Radius)}\">"
                        + $"<title>{E(shared.ClassName)} — {Count(shared.Slugs.Count, "application")}</title></circle>");
            sb.AppendLine($"      <text x=\"{N(shared.LabelX)}\" y=\"{N(shared.LabelY)}\" "
                        + $"text-anchor=\"{shared.LabelAnchor}\">{E(shared.ClassName)}</text>");
            sb.AppendLine("    </g>");
        }

        foreach (var application in map.Applications)
        {
            var outward = application.X < map.Width / 2 - 1 ? "end"
                        : application.X > map.Width / 2 + 1 ? "start"
                        : "middle";
            var labelX = application.X + (Math.Cos(application.Angle) * (application.Radius + 10));
            var labelY = application.Y + (Math.Sin(application.Angle) * (application.Radius + 10)) + 4;

            sb.AppendLine($"    <g class=\"anode\" data-slug=\"{E(application.Slug)}\" tabindex=\"0\" role=\"button\" "
                        + $"aria-label=\"{E(application.Name)}, {Count(application.EntityCount, "entity")}\">");
            sb.AppendLine($"      <circle cx=\"{N(application.X)}\" cy=\"{N(application.Y)}\" r=\"{N(application.Radius)}\">"
                        + $"<title>{E(application.Name)} — {Count(application.EntityCount, "entity")}</title></circle>");
            sb.AppendLine($"      <text x=\"{N(labelX)}\" y=\"{N(labelY)}\" text-anchor=\"{outward}\">{E(application.Name)}</text>");
            sb.AppendLine("    </g>");
        }

        sb.AppendLine("  </svg>");
        sb.AppendLine("  <div class=\"legend\">"
                    + "<span><i class=\"dot dot--app\"></i>an application — bigger means more entities</span>"
                    + "<span><i class=\"dot dot--class\"></i>a class more than one models — bigger means more of them</span>"
                    + "<span><i class=\"dot dot--mid\"></i>the centre is your common ground</span>"
                    + "</div>");
        sb.AppendLine("  </div>");
    }

    /// <summary>
    /// Bows a link toward the centre so links leaving one application fan out instead of stacking.
    /// </summary>
    private static string Bow(double fromX, double fromY, double toX, double toY, CorpusMap map)
    {
        var midX = (fromX + toX) / 2;
        var midY = (fromY + toY) / 2;

        var controlX = midX + ((map.Width / 2) - midX) * 0.18;
        var controlY = midY + ((map.Height / 2) - midY) * 0.18;

        return $"M{N(fromX)},{N(fromY)} Q{N(controlX)},{N(controlY)} {N(toX)},{N(toY)}";
    }

    private static void WriteOverlap(StringBuilder sb, OverlapGrid overlap)
    {
        if (overlap.IsEmpty || overlap.Applications.Count < 2)
            return;

        sb.AppendLine("  <h3 class=\"sub\">Which two are most alike</h3>");
        sb.AppendLine("  <p class=\"lede\">Class names both applications model. The diagonal is how many each "
                    + "has of its own. Click any cell to hold the page to just those two.</p>");

        sb.AppendLine("  <div class=\"scroller\">");
        sb.AppendLine("  <table class=\"heat\"><thead><tr><th></th>");

        // Numbered columns rather than turned-on-their-side names. The row headings carry the names
        // once, and a rotated column of twenty-character names costs a band of empty page taller
        // than the grid it labels.
        for (var index = 0; index < overlap.Applications.Count; index++)
            sb.AppendLine($"    <th class=\"app\" title=\"{E(overlap.Applications[index].Name)}\">{index + 1}</th>");

        sb.AppendLine("  </tr></thead><tbody>");

        for (var row = 0; row < overlap.Applications.Count; row++)
        {
            sb.AppendLine($"    <tr><th class=\"rowhead\"><span class=\"idx\">{row + 1}</span>"
                        + $"{E(overlap.Applications[row].Name)}</th>");

            foreach (var cell in overlap.Cells.Where(c => c.Row == row).OrderBy(c => c.Column))
            {
                if (cell.IsSelf)
                {
                    sb.AppendLine($"      <td class=\"self\" title=\"{E(overlap.Applications[row].Name)} models "
                                + $"{Count(cell.Shared, "class")} of its own\">{cell.Shared}</td>");
                    continue;
                }

                var intensity = overlap.Highest == 0 ? 0 : (double)cell.Shared / overlap.Highest;
                var rowName = overlap.Applications[cell.Row].Name;
                var columnName = overlap.Applications[cell.Column].Name;

                sb.AppendLine($"      <td class=\"cell{(cell.Shared > 0 ? " is-shared" : string.Empty)}\" "
                            + $"style=\"--i:{N(intensity)}\" data-pair=\"{E(cell.RowSlug)} {E(cell.ColumnSlug)}\" "
                            + $"data-label=\"{E(rowName)} + {E(columnName)}\" "
                            + $"title=\"{E(rowName)} and {E(columnName)} both model {Count(cell.Shared, "class")}\">"
                            + $"{(cell.Shared == 0 ? "·" : cell.Shared.ToString())}</td>");
            }

            sb.AppendLine("    </tr>");
        }

        sb.AppendLine("  </tbody></table>");
        sb.AppendLine("  </div>");
    }

    private static void WriteVersions(StringBuilder sb, VersionSpread versions)
    {
        if (versions.IsEmpty)
            return;

        sb.AppendLine("  <h3 class=\"sub\">The releases you are on</h3>");
        sb.Append("  <p class=\"lede\">");
        sb.Append(versions.IsSplit
            ? $"These applications sit on {Count(versions.Stops.Count, "different DevExpress release")}. "
            : "Every application here declares the same DevExpress release. ");
        sb.Append("Read from the project file, spaced evenly rather than to scale — nine years between two "
                + "releases would draw as one dot and a gap.");
        if (versions.Undeclared > 0)
            sb.Append($" {Count(versions.Undeclared, "application")} declares no version this could read.");
        sb.AppendLine("</p>");

        sb.AppendLine("  <div class=\"scroller\"><div class=\"spread\">");

        foreach (var stop in versions.Stops)
        {
            var here = versions.Applications.Where(a => a.Version == stop).ToList();
            var isCatalog = string.Equals(stop, versions.CatalogVersion, StringComparison.Ordinal);

            sb.AppendLine($"    <div class=\"stop{(isCatalog ? " is-catalog" : string.Empty)}\">");
            sb.AppendLine($"      <div class=\"stop__v\">{E(stop)}</div>");
            sb.AppendLine("      <div class=\"stop__rule\"></div>");

            foreach (var mark in here)
                sb.AppendLine($"      <a class=\"stop__app\" href=\"#app-{E(mark.Slug)}\">{E(mark.Name)}</a>");

            sb.AppendLine("    </div>");
        }

        sb.AppendLine("  </div></div>");

        if (!string.IsNullOrWhiteSpace(versions.CatalogVersion))
        {
            var onCatalog = versions.Applications.Count(a => a.Version == versions.CatalogVersion);

            sb.AppendLine($"  <p class=\"caveat\"><b>The framework catalog on this machine describes "
                        + $"{E(versions.CatalogVersion)}.</b> {Count(onCatalog, "application")} here declares it. "
                        + "For the rest, anything said about DevExpress types is the closest answer available "
                        + "rather than a certain one — see <a href=\"#limits\">what this cannot tell you</a>.</p>");
        }
    }

    // ------------------------------------------------------------ in common

    private static void WriteShared(StringBuilder sb, WikiCorpus corpus)
    {
        sb.AppendLine("<section id=\"shared\">");
        sb.AppendLine("  <h2>What you have built before</h2>");
        sb.AppendLine($"  <p class=\"lede\">{SharedPreamble(corpus)}</p>");

        WriteRecurringEntities(sb, corpus);
        WriteYourLayer(sb, corpus);
        WriteRecurringActions(sb, corpus);
        WriteConventions(sb, corpus);
        WriteSharedDependencies(sb, corpus);

        sb.AppendLine("</section>");
    }

    /// <summary>
    /// The sentence at the top of the corpus section, in the three states it can be in.
    /// </summary>
    /// <remarks>
    /// An empty result is a finding, not a failure, and has to read like one. The alternative — a
    /// heading followed by nothing — reads as a tool that did not run.
    /// </remarks>
    private static string SharedPreamble(WikiCorpus corpus)
    {
        if (corpus.Applications.Count < 2)
        {
            return "One application. A corpus of one has nothing to compare itself against — add "
                 + "more with <code>xaflogic projects add</code>, and this section starts answering "
                 + "the question the wiki exists for.";
        }

        var findings = corpus.ModelledTwiceCount
                     + corpus.RecurringBaseTypes.Count
                     + corpus.RecurringActions.Count
                     + corpus.Conventions.Count;

        if (findings == 0)
        {
            return $"These {corpus.Applications.Count} applications share no class name, no base "
                 + "class, no action and no property name. That is itself a finding: they were "
                 + "built as separate ideas, and there is nothing here waiting to be reused.";
        }

        return "Everything below was computed by comparing what was read from source. It is the "
             + "answer to the question nobody can answer from memory after ten years of client "
             + "work: <strong>have I built this before?</strong>";
    }

    /// <summary>
    /// The classes several applications model, and separately the ones the framework supplied.
    /// </summary>
    /// <remarks>
    /// Two headings rather than one list with a badge, because the count beside a heading is read
    /// as the size of what is under it. A single list saying "4" above six cards asks the reader
    /// to work out which two do not count, and the first number on the page is exactly the one
    /// that has to be trustworthy without arithmetic.
    /// </remarks>
    private static void WriteRecurringEntities(StringBuilder sb, WikiCorpus corpus)
    {
        var modelled = corpus.ModelledTwice.ToList();
        var templates = corpus.Templates.ToList();

        if (modelled.Count > 0)
        {
            sb.AppendLine($"  <h3 class=\"sub\">Classes modelled more than once <span class=\"card__meta\">{modelled.Count}</span></h3>");
            sb.AppendLine("  <p class=\"lede\">Matched by name. Two classes called <code>Cliente</code> in two "
                        + "applications may model different things — what is known is that they share a name. "
                        + "The comparison is what tells you whether they share a shape, and which application "
                        + "to open before writing it again.</p>");

            foreach (var recurring in modelled)
                WriteRecurringEntity(sb, recurring);
        }

        if (templates.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Carried by the framework, not modelled here <span class=\"card__meta\">{templates.Count}</span></h3>");
        sb.AppendLine("  <p class=\"lede\">Each of these implements a DevExpress security contract and is "
                    + "declared with the same properties in every application that has it — the shape the XAF "
                    + "Project Wizard writes into every solution made with v21.1 or later. Two applications "
                    + "holding the same one did not build it twice, so it is left out of the count above. "
                    + "Extend it in two of them and it moves back up, because then there is a difference "
                    + "worth reading.</p>");

        foreach (var recurring in templates)
            WriteRecurringEntity(sb, recurring);
    }

    private static void WriteRecurringEntity(StringBuilder sb, RecurringEntity recurring)
    {
        var apps = recurring.In.Select(s => s.Slug).Distinct(StringComparer.Ordinal);
        var haystack = Haystack(recurring.ClassName,
            string.Join(" ", recurring.In.Select(s => s.Application)),
            string.Join(" ", recurring.Properties.Select(p => p.Name + " " + p.TypeName)));

        // Addressable, so clicking the class on the map lands on its comparison rather than
        // typing its name into the search box and hiding everything it could be compared with.
        sb.AppendLine($"  <article class=\"card\" id=\"shared-{E(recurring.ClassName)}\" "
                    + $"data-search=\"{haystack}\" data-app=\"{string.Join(" ", apps)}\">");
        sb.AppendLine("    <div class=\"card__head\">");
        sb.AppendLine($"      <span class=\"card__name\">{E(recurring.ClassName)}</span>");
        sb.AppendLine($"      <span class=\"card__meta\">in {Count(recurring.In.Count, "application")}</span>");
        sb.AppendLine(recurring.IsTemplate
            ? "      <span class=\"pill pill--template\">framework</span>"
            : recurring.Agrees
                ? "      <span class=\"pill pill--shared\">same properties</span>"
                : "      <span class=\"pill pill--own\">shapes differ</span>");
        sb.AppendLine("    </div>");

        if (recurring.IsTemplate && recurring.Contracts.Count > 0)
        {
            // The evidence, not a restatement of the heading: which contract this class carries is
            // checkable against the file cited under the card.
            var carried = string.Join(", ", recurring.Contracts.Select(name => $"<code>{E(name)}</code>"));

            sb.AppendLine($"    <p class=\"card__desc\">Implements {carried}, and every application "
                        + "declares it with the same properties.</p>");
        }
        else if (!recurring.Agrees)
        {
            sb.AppendLine($"    <p class=\"card__desc\"><strong>{E(recurring.Richest)}</strong> models it in the "
                        + "most detail. The marked rows are the properties the others do not have.</p>");
        }

        WriteMatrix(sb, recurring);
        WriteSites(sb, recurring.In, showWeight: "property");
        sb.AppendLine("  </article>");
    }

    private static void WriteMatrix(StringBuilder sb, RecurringEntity recurring)
    {
        if (recurring.Properties.Count == 0)
            return;

        var apps = recurring.In.Select(s => s.Application).ToList();

        sb.AppendLine("    <div class=\"scroller\">");
        sb.AppendLine("    <table class=\"matrix\"><thead><tr><th>Property</th><th>Type</th>");
        foreach (var app in apps)
            sb.AppendLine($"      <th class=\"app\">{E(app)}</th>");
        sb.AppendLine("    </tr></thead><tbody>");

        foreach (var property in recurring.Properties)
        {
            var partial = property.Applications.Count < apps.Count;
            sb.Append($"      <tr{(partial ? " class=\"partial\"" : string.Empty)}>");
            sb.Append($"<td class=\"mono\">{E(property.Name)}</td>");
            sb.Append($"<td class=\"mono t\">{E(property.TypeName ?? "—")}</td>");

            foreach (var app in apps)
            {
                var has = property.Applications.Contains(app, StringComparer.Ordinal);
                sb.Append($"<td class=\"mark {(has ? "has" : "hasnt")}\">{(has ? "●" : "·")}</td>");
            }

            sb.AppendLine("</tr>");
        }

        sb.AppendLine("    </tbody></table>");
        sb.AppendLine("    </div>");
    }

    private static void WriteYourLayer(StringBuilder sb, WikiCorpus corpus)
    {
        if (corpus.RecurringBaseTypes.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">The layer you wrote yourself <span class=\"card__meta\">{corpus.RecurringBaseTypes.Count}</span></h3>");
        sb.AppendLine("  <p class=\"lede\">Base classes carried from one application into another: your own "
                    + "framework, the one that was never written down. A base class earns a place here only "
                    + "when its own source was read in one of these applications — no list of DevExpress type "
                    + "names is involved, so nothing here goes stale when DevExpress renames something.</p>");

        foreach (var reused in corpus.RecurringBaseTypes)
        {
            var apps = reused.In.Select(s => s.Slug).Append(reused.DeclaredAt.Slug).Distinct(StringComparer.Ordinal);
            var haystack = Haystack(reused.Name, string.Join(" ", reused.In.Select(s => s.Application)));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{string.Join(" ", apps)}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(reused.Name)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{Count(reused.TotalDerived, reused.Kind == BaseTypeKind.Entity ? "class" : "controller")} "
                        + $"across {Count(reused.In.Count, "application")}</span>");
            sb.AppendLine($"      <span class=\"pill pill--shared\">{(reused.Kind == BaseTypeKind.Entity ? "entity base" : "controller base")}</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine($"    <p class=\"card__desc\">Written in <strong>{E(reused.DeclaredAt.Application)}</strong>"
                        + $"{Cite(reused.DeclaredAt.Citation)}.</p>");
            WriteSites(sb, reused.In, showWeight: reused.Kind == BaseTypeKind.Entity ? "class" : "controller");
            sb.AppendLine("  </article>");
        }
    }

    private static void WriteRecurringActions(StringBuilder sb, WikiCorpus corpus)
    {
        if (corpus.RecurringActions.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Actions written more than once <span class=\"card__meta\">{corpus.RecurringActions.Count}</span></h3>");
        sb.AppendLine("  <p class=\"lede\">The same operation implemented in more than one application. Whether "
                    + "that is duplication or convention is a judgement only you can make — the wiki only "
                    + "puts the implementations next to each other.</p>");

        foreach (var recurring in corpus.RecurringActions)
        {
            var apps = recurring.In.Select(s => s.Slug).Distinct(StringComparer.Ordinal).ToList();
            var haystack = Haystack(recurring.ActionId, recurring.Caption,
                string.Join(" ", recurring.In.Select(s => s.Application + " " + s.Owner)));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{string.Join(" ", apps)}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(recurring.ActionId)}</span>");
            if (!string.IsNullOrWhiteSpace(recurring.Caption))
                sb.AppendLine($"      <span class=\"pill\">“{E(recurring.Caption)}”</span>");
            sb.AppendLine($"      <span class=\"card__meta\">in {Count(apps.Count, "application")}</span>");
            sb.AppendLine("    </div>");
            WriteSites(sb, recurring.In, showWeight: null);
            sb.AppendLine("  </article>");
        }
    }

    private static void WriteConventions(StringBuilder sb, WikiCorpus corpus)
    {
        if (corpus.Conventions.Count == 0)
            return;

        // Three groups, not two. A name that is a decimal here and a double there is a defect
        // waiting to happen; a name that is one collection here and another there is a word doing
        // its ordinary job. Listing them together buries the first under the second.
        var conflicting = corpus.Conventions.Where(c => c.ScalarConflict).ToList();
        var vocabulary = corpus.Conventions.Where(c => !c.ScalarConflict).ToList();

        if (conflicting.Count > 0)
        {
            sb.AppendLine($"  <h3 class=\"sub\">The same name, two shapes <span class=\"card__meta\">{conflicting.Count}</span></h3>");
            sb.AppendLine("  <p class=\"lede\">A property name that means one scalar type in one application and "
                        + "a different one in another. Nothing is broken — each application compiles — but a "
                        + "reader moving between them will assume the first shape they learned, and a "
                        + "<code>decimal</code> that is a <code>double</code> next door is how a total ends up "
                        + "two cents out.</p>");

            foreach (var convention in conflicting)
                WriteConvention(sb, convention);
        }

        if (vocabulary.Count > 0)
        {
            sb.AppendLine($"  <h3 class=\"sub\">Names you keep <span class=\"card__meta\">{vocabulary.Count}</span></h3>");
            sb.AppendLine("  <p class=\"lede\">Property names used in more than one application. This is the "
                        + "vocabulary of your applications, which nobody wrote down and everybody who joins has "
                        + "to learn by reading code.</p>");

            foreach (var convention in vocabulary)
                WriteConvention(sb, convention);
        }

        if (corpus.ConventionsNotShown > 0)
        {
            sb.AppendLine($"  <p class=\"lede\">{corpus.ConventionsNotShown} further shared names are not listed. "
                        + "The list is ranked by how many applications use a name, and the tail is one-offs "
                        + "rather than habits.</p>");
        }
    }

    private static void WriteConvention(StringBuilder sb, RecurringProperty convention)
    {
        var apps = convention.In.Select(s => s.Slug).Distinct(StringComparer.Ordinal);
        var haystack = Haystack(convention.Name, convention.TypeName,
            string.Join(" ", convention.ConflictingTypes),
            string.Join(" ", convention.In.Select(s => s.Application)));

        sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{string.Join(" ", apps)}\">");
        sb.AppendLine("    <div class=\"card__head\">");
        sb.AppendLine($"      <span class=\"card__name\">{E(convention.Name)}</span>");

        if (convention.Consistent)
        {
            var shape = convention.TypeName ?? "—";
            if (convention.Size is > 0) shape += $"({convention.Size})";
            sb.AppendLine($"      <span class=\"pill pill--shared\">{E(shape)}</span>");
        }
        else if (convention.ScalarConflict)
        {
            // Scalars first and only a few of them: a name held by eleven per-entity enums plus one
            // string is on this list because of the string, and a row of eleven enum names hides it.
            var shown = convention.ConflictingTypes
                .OrderByDescending(CorpusAnalyzer.IsScalar)
                .Take(4)
                .ToList();

            foreach (var type in shown)
                sb.AppendLine($"      <span class=\"pill pill--own\">{E(type)}</span>");

            var rest = convention.ConflictingTypes.Count - shown.Count;

            if (rest > 0)
                sb.AppendLine($"      <span class=\"pill\">+{rest} more</span>");
        }
        else
        {
            // The name is the convention; what it holds is per-entity by design. Printing seven
            // collection types here would say "inconsistent" about something that is not.
            sb.AppendLine($"      <span class=\"pill\">{convention.ConflictingTypes.Count} types, one per entity</span>");
        }

        sb.AppendLine($"      <span class=\"card__meta\">{Count(convention.TotalUses, "class")} "
                    + $"across {Count(convention.In.Count, "application")}</span>");
        sb.AppendLine("    </div>");
        WriteSites(sb, convention.In, showWeight: "class");
        sb.AppendLine("  </article>");
    }

    private static void WriteSharedDependencies(StringBuilder sb, WikiCorpus corpus)
    {
        var modules = corpus.SharedDependencies.Where(d => d.Kind == DependencyKind.RequiredModule).ToList();

        if (modules.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Modules more than one application requires <span class=\"card__meta\">{modules.Count}</span></h3>");
        sb.AppendLine("  <p class=\"lede\">Read from <code>RequiredModuleTypes</code>. A module every application "
                    + "requires is part of how you build, not a choice made for one client.</p>");

        foreach (var module in modules)
        {
            var haystack = Haystack(module.Name, string.Join(" ", module.Applications));
            var slugs = corpus.Applications
                .Where(a => module.Applications.Contains(a.Name, StringComparer.Ordinal))
                .Select(a => a.Slug);

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{string.Join(" ", slugs)}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(module.Name)}</span>");
            sb.AppendLine(module.Universal
                ? "      <span class=\"pill pill--shared\">every application</span>"
                : $"      <span class=\"card__meta\">{Count(module.Applications.Count, "application")}</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine($"    <p class=\"card__desc\">{E(string.Join(" · ", module.Applications))}</p>");
            sb.AppendLine("  </article>");
        }
    }

    /// <summary>
    /// The applications a finding was found in, each counted by <paramref name="showWeight"/> --
    /// which arrives <em>singular</em> and is pluralised on the way out.
    /// </summary>
    private static void WriteSites(StringBuilder sb, IReadOnlyList<CorpusSite> sites, string? showWeight)
    {
        if (sites.Count == 0)
            return;

        sb.AppendLine("    <div class=\"sites\">");

        foreach (var site in sites)
        {
            sb.Append($"      <span class=\"site\"><a href=\"#app-{E(site.Slug)}\"><b>{E(site.Application)}</b></a>");

            // Through Count, not interpolated: the nouns arrive singular so that a site holding
            // one of something does not read "1 properties" beside a heading that got it right.
            if (showWeight is not null && site.Weight > 0)
                sb.Append($" · {E(Count(site.Weight, showWeight))}");

            if (!string.IsNullOrWhiteSpace(site.Owner) && showWeight is null)
                sb.Append($" · {E(site.Owner)}");

            sb.Append(Cite(site.Citation));
            sb.AppendLine("</span>");
        }

        sb.AppendLine("    </div>");
    }

    private static string Cite(string? citation) =>
        string.IsNullOrWhiteSpace(citation) ? string.Empty : $" <span class=\"cite\">{E(citation)}</span>";

    // ------------------------------------------------------- the applications

    private static void WriteApplications(StringBuilder sb, WikiCorpus corpus)
    {
        sb.AppendLine("<section id=\"applications\">");
        sb.AppendLine($"  <h2>The applications <span class=\"card__meta\">{corpus.Applications.Count}</span></h2>");
        sb.AppendLine("  <div class=\"apps\">");

        foreach (var app in corpus.Applications)
        {
            var project = app.Project;
            var haystack = Haystack(app.Name, project.ProjectName, project.ProjectPath, project.OrmType);

            sb.AppendLine($"    <a class=\"card appcard\" href=\"#app-{E(app.Slug)}\" data-search=\"{haystack}\" data-app=\"{E(app.Slug)}\">");
            sb.AppendLine($"      <h3>{E(app.Name)}</h3>");
            sb.AppendLine($"      <div class=\"path\">{E(project.ProjectPath)}</div>");
            sb.Append("      <div class=\"counts\">");
            sb.Append($"{project.Entities.Count} entities · {project.Controllers.Count} controllers · {app.ActionCount} actions");
            if (project.Reports.Count > 0) sb.Append($" · {project.Reports.Count} reports");
            sb.AppendLine("</div>");
            sb.Append("      <div class=\"counts\">");
            sb.Append($"<span class=\"pill\">{E(Orm.Label(project.OrmType))}</span> ");
            if (!string.IsNullOrWhiteSpace(project.TargetFramework))
                sb.Append($"<span class=\"pill\">{E(project.TargetFramework)}</span> ");
            if (!string.IsNullOrWhiteSpace(project.DeclaredDevExpressVersion))
                sb.Append($"<span class=\"pill pill--fw\">DevExpress {E(project.DeclaredDevExpressVersion)}</span>");
            sb.AppendLine("</div>");
            sb.AppendLine("    </a>");
        }

        sb.AppendLine("  </div>");
        sb.AppendLine("</section>");
    }

    private static void WriteApplication(StringBuilder sb, WikiApplication app)
    {
        var project = app.Project;

        sb.AppendLine($"<section id=\"app-{E(app.Slug)}\">");
        sb.AppendLine($"  <h2>{E(app.Name)}</h2>");
        sb.Append("  <p class=\"lede\">");
        sb.Append($"Persisted with <strong>{E(Orm.Label(project.OrmType))}</strong>");
        if (!string.IsNullOrWhiteSpace(project.TargetFramework))
            sb.Append($" on {E(project.TargetFramework)}");
        sb.Append(". For the full picture of this one application on its own, ");
        sb.AppendLine($"<code>xaflogic explain --project {E(project.ProjectPath)}</code>.</p>");

        WriteApplicationEntities(sb, app);
        WriteApplicationOperations(sb, app);
        WriteApplicationReports(sb, app);
        WriteApplicationNavigation(sb, app);

        sb.AppendLine("</section>");
    }

    private static void WriteApplicationEntities(StringBuilder sb, WikiApplication app)
    {
        var entities = app.Project.Entities;

        if (entities.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Entities <span class=\"card__meta\">{entities.Count}</span></h3>");

        foreach (var entity in entities.OrderBy(e => e.ClassName, StringComparer.Ordinal))
        {
            var declared = entity.Properties.Where(p => p.InheritedFrom is null).ToList();
            var haystack = Haystack(entity.ClassName, entity.Description, entity.BaseType,
                string.Join(" ", entity.Properties.Select(p => p.Name + " " + p.TypeName)),
                string.Join(" ", entity.Relationships.Select(r => r.RelatedEntity)));

            sb.AppendLine($"  <details class=\"card\" id=\"{E(app.Slug)}-entity-{E(entity.ClassName)}\" "
                        + $"data-search=\"{haystack}\" data-app=\"{E(app.Slug)}\">");
            sb.Append($"    <summary><span class=\"card__name\">{E(entity.ClassName)}</span> ");
            sb.Append($"<span class=\"card__meta\">{E(entity.BaseType)} · {declared.Count} declared properties");
            if (entity.Relationships.Count > 0) sb.Append($" · {entity.Relationships.Count} relationships");
            sb.Append("</span>");
            // Where it lives. A wiki entry that cannot tell you which file to open has sent you
            // back to searching, which is the thing it exists to replace.
            sb.Append(Cite(SourceCitation.Of(app.Project, entity.FilePath, entity.Line)));
            sb.AppendLine("</summary>");

            if (!string.IsNullOrWhiteSpace(entity.Description))
                sb.AppendLine($"    <p class=\"card__desc\">{E(entity.Description)}</p>");

            if (declared.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Property</th><th>Type</th><th></th></tr></thead><tbody>");
                foreach (var property in declared)
                {
                    sb.Append($"      <tr><td class=\"mono\">{E(property.Name)}</td>");
                    sb.Append($"<td class=\"mono t\">{E(property.TypeName)}</td><td>");
                    if (property.IsKey) sb.Append("<span class=\"pill pill--key\">key</span> ");
                    if (property.IsRequired) sb.Append("<span class=\"pill pill--req\">required</span> ");
                    if (property.IsUnique) sb.Append("<span class=\"pill pill--req\">unique</span> ");
                    if (!string.IsNullOrWhiteSpace(property.PersistentAlias)) sb.Append("<span class=\"pill pill--calc\">calculated</span> ");
                    if (property.Size is > 0) sb.Append($"<span class=\"pill\">max {property.Size}</span> ");
                    sb.AppendLine("</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            if (entity.Relationships.Count > 0)
            {
                sb.AppendLine("    <table><thead><tr><th>Relationship</th><th>To</th><th></th></tr></thead><tbody>");
                foreach (var relationship in entity.Relationships)
                {
                    sb.Append($"      <tr><td class=\"mono\">{E(relationship.PropertyName)}</td>");
                    sb.Append($"<td class=\"mono\"><a href=\"#{E(app.Slug)}-entity-{E(relationship.RelatedEntity)}\">{E(relationship.RelatedEntity)}</a></td><td>");
                    if (relationship.IsAggregated) sb.Append("<span class=\"pill pill--own\">owned</span> ");
                    sb.AppendLine("</td></tr>");
                }
                sb.AppendLine("    </tbody></table>");
            }

            sb.AppendLine("  </details>");
        }
    }

    private static void WriteApplicationOperations(StringBuilder sb, WikiApplication app)
    {
        var controllers = app.Project.Controllers.Where(c => c.Actions.Count > 0).ToList();

        if (controllers.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Operations <span class=\"card__meta\">{app.ActionCount}</span></h3>");

        foreach (var controller in controllers.OrderBy(c => c.ClassName, StringComparer.Ordinal))
        {
            var haystack = Haystack(controller.ClassName, controller.TargetObjectType,
                controller.BusinessLogicSummary,
                string.Join(" ", controller.Actions.Select(a => a.ActionId + " " + a.Caption)));

            sb.AppendLine($"  <details class=\"card\" data-search=\"{haystack}\" data-app=\"{E(app.Slug)}\">");
            sb.Append($"    <summary><span class=\"card__name\">{E(controller.ClassName)}</span> ");
            sb.Append($"<span class=\"card__meta\">{Count(controller.Actions.Count, "action")}");
            if (!string.IsNullOrWhiteSpace(controller.TargetObjectType))
                sb.Append($" · on {E(controller.TargetObjectType)}");
            sb.Append("</span>");
            sb.Append(Cite(SourceCitation.Of(app.Project, controller.FilePath, controller.Line)));
            sb.AppendLine("</summary>");

            if (!string.IsNullOrWhiteSpace(controller.BusinessLogicSummary))
                sb.AppendLine($"    <p class=\"card__desc\">{E(controller.BusinessLogicSummary)}</p>");

            sb.AppendLine("    <table><thead><tr><th>Action</th><th>Shown as</th><th>Only when</th></tr></thead><tbody>");
            foreach (var action in controller.Actions)
            {
                sb.Append($"      <tr><td class=\"mono\">{E(action.ActionId)}</td>");
                sb.Append($"<td class=\"t\">{E(action.Caption ?? "—")}</td><td class=\"t\">");
                sb.Append(string.IsNullOrWhiteSpace(action.EnabledCriteria)
                    ? "always"
                    : $"<code class=\"crit\">{E(action.EnabledCriteria)}</code>");
                sb.AppendLine("</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
            sb.AppendLine("  </details>");
        }
    }

    private static void WriteApplicationReports(StringBuilder sb, WikiApplication app)
    {
        var project = app.Project;

        if (project.Reports.Count == 0 && !project.ReferencesReportsModule)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Reports <span class=\"card__meta\">{project.Reports.Count}</span></h3>");

        var haystack = Haystack("reports", string.Join(" ", project.Reports.Select(r => r.DisplayName + " " + r.DataType)));
        sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{E(app.Slug)}\">");

        if (project.Reports.Count > 0)
        {
            sb.AppendLine("    <table><thead><tr><th>Report</th><th>Over</th><th>Dialog</th></tr></thead><tbody>");
            foreach (var report in project.Reports)
            {
                sb.Append($"      <tr><td>{E(report.DisplayName)}</td>");
                sb.Append($"<td class=\"mono\">{E(report.DataType)}</td>");
                sb.AppendLine($"<td class=\"mono t\">{E(report.ParametersType ?? "—")}</td></tr>");
            }
            sb.AppendLine("    </tbody></table>");
        }

        if (project.ReferencesReportsModule)
        {
            sb.AppendLine("    <p class=\"caveat\"><b>A lower bound.</b> This application registers the reports "
                        + "module, so users can build their own at run time. Those layouts live in the database, "
                        + "where reading source cannot see them. The number above is what is declared in code, "
                        + "not how many reports exist.</p>");
        }

        sb.AppendLine("  </article>");
    }

    private static void WriteApplicationNavigation(StringBuilder sb, WikiApplication app)
    {
        var navigation = app.Project.Navigation;

        if (navigation.Count == 0)
            return;

        sb.AppendLine($"  <h3 class=\"sub\">Navigation <span class=\"card__meta\">{navigation.Count}</span></h3>");

        foreach (var group in navigation)
        {
            var haystack = Haystack(group.GroupName, string.Join(" ", group.EntityClassNames));

            sb.AppendLine($"  <article class=\"card\" data-search=\"{haystack}\" data-app=\"{E(app.Slug)}\">");
            sb.AppendLine("    <div class=\"card__head\">");
            sb.AppendLine($"      <span class=\"card__name\">{E(group.GroupName)}</span>");
            sb.AppendLine($"      <span class=\"card__meta\">{Count(group.EntityClassNames.Count, "item")}</span>");
            sb.AppendLine("    </div>");
            sb.AppendLine($"    <p class=\"card__desc mono\">{E(string.Join(" · ", group.EntityClassNames))}</p>");
            sb.AppendLine("  </article>");
        }
    }

    // ------------------------------------------------------------- the limits

    private static void WriteLimits(StringBuilder sb, WikiCorpus corpus)
    {
        sb.AppendLine("<section id=\"limits\">");
        sb.AppendLine("  <h2>What this cannot tell you</h2>");
        sb.AppendLine("  <p class=\"lede\">Everything above was read from source without compiling anything. "
                    + "That is what lets it run against a nine-year-old project whose packages no longer "
                    + "restore, and it is also the reason for each of the following.</p>");

        sb.AppendLine("  <article class=\"card\" data-search=\"limits names matching cliente\">");
        sb.AppendLine("    <div class=\"card__head\"><span class=\"card__name\">Two classes sharing a name may share nothing else</span></div>");
        sb.AppendLine("    <p class=\"card__desc\">Classes are matched across applications by name, because that is "
                    + "all source can offer — there is no shared identity between two <code>Cliente</code> classes "
                    + "in two different solutions. The property comparison is there so you can see for yourself "
                    + "whether they are the same idea.</p>");
        sb.AppendLine("  </article>");

        sb.AppendLine("  <article class=\"card\" data-search=\"limits base classes libraries shared\">");
        sb.AppendLine("    <div class=\"card__head\"><span class=\"card__name\">A base class in a library you did not add is missing</span></div>");
        sb.AppendLine("    <p class=\"card__desc\">A base class is listed as yours only when its own source was read "
                    + "in one of these applications. If your shared layer lives in a compiled library outside every "
                    + "project here, its classes are absent from <em>The layer you wrote yourself</em> — not because "
                    + "they are the framework, but because nobody opened them. Add that project to the wiki and "
                    + "they appear.</p>");
        sb.AppendLine("  </article>");

        var withReports = corpus.Applications.Where(a => a.Project.ReferencesReportsModule).ToList();

        if (withReports.Count > 0)
        {
            sb.AppendLine($"  <article class=\"card\" data-search=\"limits reports database lower bound\" "
                        + $"data-app=\"{string.Join(" ", withReports.Select(a => a.Slug))}\">");
            sb.AppendLine("    <div class=\"card__head\"><span class=\"card__name\">Report counts are lower bounds</span></div>");
            sb.AppendLine($"    <p class=\"card__desc\">{Count(withReports.Count, "application")} here register the "
                        + "reports module, so users can build reports at run time. Those live in the database. "
                        + "The number is unknown, not zero.</p>");
            sb.AppendLine($"    <p class=\"card__desc\">{E(string.Join(" · ", withReports.Select(a => a.Name)))}</p>");
            sb.AppendLine("  </article>");
        }

        foreach (var app in corpus.Applications)
        {
            var caveat = CatalogTrust.Caveat(app.Project);

            if (string.IsNullOrWhiteSpace(caveat))
                continue;

            sb.AppendLine($"  <article class=\"card\" data-search=\"limits catalog devexpress version {app.Name.ToLowerInvariant()}\" "
                        + $"data-app=\"{E(app.Slug)}\">");
            sb.AppendLine($"    <div class=\"card__head\"><span class=\"card__name\">{E(app.Name)}</span>"
                        + "<span class=\"card__meta\">framework claims</span></div>");
            sb.AppendLine($"    <p class=\"caveat\">{E(caveat)}</p>");
            sb.AppendLine("  </article>");
        }

        sb.AppendLine("</section>");
    }

    private void WriteFooter(StringBuilder sb, WikiCorpus corpus)
    {
        var when = corpus.Applications.Count > 0
            ? corpus.Applications.Max(a => a.Project.ExtractedAt)
            : DateTime.UtcNow.ToString("o");

        sb.AppendLine("<footer><div class=\"wrap\">");
        sb.AppendLine($"  <p>Read from source by <a href=\"https://github.com/peopleworks/XAFLogicExplainer\">XAF Logic Explainer</a> "
                    + $"{E(_toolVersion)}. Nothing on this page was written by hand.</p>");
        sb.AppendLine($"  <p>Most recent extraction: {E(when)}. Rebuild with <code>xaflogic wiki</code>.</p>");
        sb.AppendLine("</div></footer>");
    }

    // ------------------------------------------------------------------ bits

    private static string Count(int value, string noun)
    {
        if (value == 1)
            return $"{value} {noun}";

        // Enough English to cover the nouns this page counts. "entity" reaching the page as
        // "entitys" is the kind of thing a reader takes as evidence that nobody looked.
        var plural =
            noun.EndsWith('y') && noun.Length > 1 && !"aeiou".Contains(noun[^2]) ? noun[..^1] + "ies"
            : noun.EndsWith('s') || noun.EndsWith('x') || noun.EndsWith("ch", StringComparison.Ordinal)
              || noun.EndsWith("sh", StringComparison.Ordinal) ? noun + "es"
            : noun + "s";

        return $"{value} {plural}";
    }

    private static string N(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    private static string Haystack(params string?[] parts) =>
        E(string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))).ToLowerInvariant());

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
