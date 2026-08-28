using System.Text;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// Reads several extracted applications and works out what they have in common.
/// </summary>
/// <remarks>
/// The question this answers is the one a developer with a decade of client work cannot answer from
/// memory: <em>have I built this before?</em> Every finding is derived from what extraction read.
/// There is no place to write a corpus fact by hand, which is deliberate — a hand-written summary of
/// nine applications is wrong the day the tenth is added, and nobody notices.
/// </remarks>
public static class CorpusAnalyzer
{
    /// <summary>
    /// Conventions are ranked and capped, because the tail is one-offs rather than habits.
    /// </summary>
    private const int ConventionLimit = 60;

    /// <summary>
    /// Base names that say only "this is a controller", which the reader already knows.
    /// </summary>
    private static readonly HashSet<string> GenericControllerBases = new(StringComparer.Ordinal)
    {
        "Controller",
        "ViewController",
        "ObjectViewController",
        "WindowController",
    };

    /// <summary>
    /// The BCL spellings of the types C# has a keyword for.
    /// </summary>
    /// <remarks>
    /// Source can say <c>Double</c> or <c>double</c> for the same type, and both spellings turn up
    /// in one corpus. Reporting that as "the same name, two shapes" is a false accusation, and a
    /// tool that makes one of those stops being believed about the true ones.
    /// </remarks>
    private static readonly Dictionary<string, string> TypeAliases = new(StringComparer.Ordinal)
    {
        ["Boolean"] = "bool",
        ["Byte"] = "byte",
        ["SByte"] = "sbyte",
        ["Char"] = "char",
        ["Decimal"] = "decimal",
        ["Double"] = "double",
        ["Single"] = "float",
        ["Int16"] = "short",
        ["UInt16"] = "ushort",
        ["Int32"] = "int",
        ["UInt32"] = "uint",
        ["Int64"] = "long",
        ["UInt64"] = "ulong",
        ["String"] = "string",
        ["Object"] = "object",
    };

    /// <summary>
    /// Value types where being nullable is a modelling decision rather than an annotation.
    /// </summary>
    private static readonly HashSet<string> ScalarValueTypes = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "char", "decimal", "double", "float",
        "short", "ushort", "int", "uint", "long", "ulong",
        "DateTime", "DateTimeOffset", "TimeSpan", "Guid",
    };

    /// <summary>
    /// Reads the applications together.
    /// </summary>
    /// <param name="applications">The applications, in the order they should be shown.</param>
    public static WikiCorpus Analyze(IReadOnlyList<WikiApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var recurring = RecurringEntities(applications);

        // The scaffold is settled once, here, and every other finding reads that answer. Deciding
        // it twice is how the classes card and the vocabulary card end up disagreeing about the
        // same two classes.
        var templates = recurring
            .Where(r => r.IsTemplate)
            .Select(r => r.ClassName)
            .ToHashSet(StringComparer.Ordinal);

        var conventions = Conventions(applications, templates);

        return new WikiCorpus
        {
            Applications = applications,
            RecurringEntities = recurring,
            RecurringBaseTypes = RecurringBaseTypes(applications),
            RecurringActions = RecurringActions(applications),
            Conventions = conventions.Take(ConventionLimit).ToList(),
            ConventionsNotShown = Math.Max(0, conventions.Count - ConventionLimit),
            SharedDependencies = SharedDependencies(applications),
        };
    }

    /// <summary>
    /// Turns a display name into an identifier usable as an anchor, unique within the corpus.
    /// </summary>
    public static string Slug(string name, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        var sb = new StringBuilder();
        var lastWasDash = true;

        foreach (var ch in name ?? string.Empty)
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');

        if (slug.Length == 0)
            slug = "app";

        var candidate = slug;

        for (var n = 2; !taken.Add(candidate); n++)
            candidate = $"{slug}-{n}";

        return candidate;
    }

    // ------------------------------------------------------------------
    // Classes modelled more than once
    // ------------------------------------------------------------------

    private static List<RecurringEntity> RecurringEntities(IReadOnlyList<WikiApplication> apps)
    {
        var byName = new Dictionary<string, List<(WikiApplication App, ExtractedEntity Entity)>>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            // One application naming the same class twice is a partial class or a namespace clash;
            // either way it is one idea, and counting it twice would inflate the corpus.
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entity in app.Project.Entities)
            {
                if (string.IsNullOrWhiteSpace(entity.ClassName) || !seen.Add(entity.ClassName))
                    continue;

                if (!byName.TryGetValue(entity.ClassName, out var list))
                    byName[entity.ClassName] = list = [];

                list.Add((app, entity));
            }
        }

        var results = new List<RecurringEntity>();

        foreach (var (className, uses) in byName)
        {
            if (uses.Count < 2)
                continue;

            var sites = uses
                .Select(u => new CorpusSite
                {
                    Application = u.App.Name,
                    Slug = u.App.Slug,
                    Owner = u.Entity.Namespace,
                    Citation = SourceCitation.Of(u.App.Project, u.Entity.FilePath, u.Entity.Line),
                    Weight = Declared(u.Entity).Count,
                })
                .OrderByDescending(s => s.Weight)
                .ThenBy(s => s.Application, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var properties = CompareProperties(uses);

            results.Add(new RecurringEntity
            {
                ClassName = className,
                In = sites,
                Properties = properties,

                // Every application has to carry the contract, and they all have to declare the
                // same properties. One application that extended it is enough to make the class a
                // real finding, because then the applications disagree -- and the disagreement is
                // the answer to "have I built this before".
                IsTemplate = uses.TrueForAll(u => SecurityContract.IsCarriedBy(u.Entity))
                             && properties.All(p => p.Applications.Count == uses.Count),

                Contracts = [.. uses
                    .SelectMany(u => SecurityContract.CarriedBy(u.Entity))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)],
            });
        }

        return results
            // Templates last: they are context, not findings, and the findings have to lead.
            .OrderBy(r => r.IsTemplate)
            .ThenByDescending(r => r.In.Count)
            .ThenByDescending(r => r.Properties.Count)
            .ThenBy(r => r.ClassName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Lines up the property names of one class as each application declares it.
    /// </summary>
    /// <remarks>
    /// Declared properties only. An entity carries what it inherits so a reader of one entity is
    /// told the whole truth, but folding inherited members into this comparison would report a
    /// shared base class as agreement between two applications that agree on nothing else.
    /// </remarks>
    private static List<PropertyPresence> CompareProperties(
        List<(WikiApplication App, ExtractedEntity Entity)> uses)
    {
        var order = new List<string>();
        var byProperty = new Dictionary<string, List<(string App, string Type)>>(StringComparer.Ordinal);

        foreach (var (app, entity) in uses)
        {
            foreach (var property in Declared(entity))
            {
                if (!byProperty.TryGetValue(property.Name, out var list))
                {
                    byProperty[property.Name] = list = [];
                    order.Add(property.Name);
                }

                list.Add((app.Name, NormalizeType(property.TypeName)));
            }
        }

        return order
            .Select(name =>
            {
                var list = byProperty[name];
                var types = list.Select(u => u.Type).Distinct(StringComparer.Ordinal).ToList();

                return new PropertyPresence
                {
                    Name = name,
                    Applications = list.Select(u => u.App).Distinct(StringComparer.Ordinal).ToList(),
                    TypeName = types.Count == 1 ? types[0] : null,
                };
            })
            .OrderByDescending(p => p.Applications.Count)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Base classes written here and reused
    // ------------------------------------------------------------------

    private static List<RecurringBaseType> RecurringBaseTypes(IReadOnlyList<WikiApplication> apps)
    {
        var results = new List<RecurringBaseType>();

        results.AddRange(Reused(
            apps,
            BaseTypeKind.Entity,
            app => app.Project.Entities.Select(e => (Name: e.ClassName, Base: StripGenerics(e.BaseType))),
            app => app.Project.Entities.Select(e => (e.ClassName, e.FilePath, e.Line, Owner: e.Namespace))));

        results.AddRange(Reused(
            apps,
            BaseTypeKind.Controller,
            app => app.Project.Controllers
                .Where(c => c.FrameworkBaseType is null)
                .Select(c => (Name: c.ClassName, Base: StripGenerics(c.BaseControllerType))),
            app => app.Project.Controllers.Select(c => (c.ClassName, c.FilePath, c.Line, Owner: c.Namespace))));

        return results
            .OrderByDescending(r => r.In.Count)
            .ThenByDescending(r => r.TotalDerived)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<RecurringBaseType> Reused(
        IReadOnlyList<WikiApplication> apps,
        BaseTypeKind kind,
        Func<WikiApplication, IEnumerable<(string Name, string Base)>> derivations,
        Func<WikiApplication, IEnumerable<(string ClassName, string FilePath, int Line, string Owner)>> declarations)
    {
        // Where the class itself was read. A base type nobody in the corpus declares is either the
        // framework or a library we never opened, and in both cases it is not this developer's layer.
        var declaredAt = new Dictionary<string, CorpusSite>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            foreach (var declaration in declarations(app))
            {
                if (string.IsNullOrWhiteSpace(declaration.ClassName))
                    continue;

                declaredAt.TryAdd(declaration.ClassName, new CorpusSite
                {
                    Application = app.Name,
                    Slug = app.Slug,
                    Owner = declaration.Owner,
                    Citation = SourceCitation.Of(app.Project, declaration.FilePath, declaration.Line),
                });
            }
        }

        var counts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            foreach (var (name, baseName) in derivations(app))
            {
                if (string.IsNullOrWhiteSpace(baseName) || baseName == name)
                    continue;

                if (kind == BaseTypeKind.Controller && GenericControllerBases.Contains(baseName))
                    continue;

                if (!declaredAt.ContainsKey(baseName))
                    continue;

                if (!counts.TryGetValue(baseName, out var perApp))
                    counts[baseName] = perApp = new Dictionary<string, int>(StringComparer.Ordinal);

                perApp[app.Slug] = perApp.GetValueOrDefault(app.Slug) + 1;
            }
        }

        var results = new List<RecurringBaseType>();

        foreach (var (baseName, perApp) in counts)
        {
            if (perApp.Count < 2)
                continue;

            var sites = apps
                .Where(a => perApp.ContainsKey(a.Slug))
                .Select(a => new CorpusSite
                {
                    Application = a.Name,
                    Slug = a.Slug,
                    Weight = perApp[a.Slug],
                })
                .OrderByDescending(s => s.Weight)
                .ThenBy(s => s.Application, StringComparer.OrdinalIgnoreCase)
                .ToList();

            results.Add(new RecurringBaseType
            {
                Name = baseName,
                Kind = kind,
                In = sites,
                DeclaredAt = declaredAt[baseName],
            });
        }

        return results;
    }

    // ------------------------------------------------------------------
    // Actions written more than once
    // ------------------------------------------------------------------

    private static List<RecurringAction> RecurringActions(IReadOnlyList<WikiApplication> apps)
    {
        var byId = new Dictionary<string, List<(WikiApplication App, ExtractedController Controller, ExtractedAction Action)>>(
            StringComparer.Ordinal);

        foreach (var app in apps)
        {
            foreach (var controller in app.Project.Controllers)
            {
                foreach (var action in controller.Actions)
                {
                    if (string.IsNullOrWhiteSpace(action.ActionId))
                        continue;

                    if (!byId.TryGetValue(action.ActionId, out var list))
                        byId[action.ActionId] = list = [];

                    list.Add((app, controller, action));
                }
            }
        }

        var results = new List<RecurringAction>();

        foreach (var (actionId, uses) in byId)
        {
            if (uses.Select(u => u.App.Slug).Distinct(StringComparer.Ordinal).Count() < 2)
                continue;

            var captions = uses
                .Select(u => u.Action.Caption)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            results.Add(new RecurringAction
            {
                ActionId = actionId,
                Caption = captions.Count == 1 ? captions[0] : null,
                In = uses
                    .Select(u => new CorpusSite
                    {
                        Application = u.App.Name,
                        Slug = u.App.Slug,
                        Owner = u.Controller.ClassName,
                        Citation = SourceCitation.Of(u.App.Project, u.Action.FilePath, u.Action.Line),
                    })
                    .OrderBy(s => s.Application, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            });
        }

        return results
            .OrderByDescending(r => r.In.Select(s => s.Slug).Distinct(StringComparer.Ordinal).Count())
            .ThenBy(r => r.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Names that keep coming back
    // ------------------------------------------------------------------

    /// <param name="apps">The applications, in the order they should be shown.</param>
    /// <param name="templates">
    /// Classes the framework supplied rather than the author, whose properties are its vocabulary
    /// and not the author own.
    /// </param>
    private static List<RecurringProperty> Conventions(
        IReadOnlyList<WikiApplication> apps,
        HashSet<string> templates)
    {
        var byName = new Dictionary<string, List<(WikiApplication App, ExtractedProperty Property)>>(StringComparer.Ordinal);

        foreach (var app in apps)
        {
            foreach (var entity in app.Project.Entities)
            {
                // A class that was not counted as modelled twice cannot supply words that were.
                // Without this, two applications sharing nothing but the wizard scaffold are told
                // that LoginProviderName and ProviderUserKey are house vocabulary -- the same
                // false claim as the classes card, two headings further down. A scaffold somebody
                // extended is not in this set, so the properties they added still count.
                if (templates.Contains(entity.ClassName))
                    continue;

                foreach (var property in Declared(entity))
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;

                    if (!byName.TryGetValue(property.Name, out var list))
                        byName[property.Name] = list = [];

                    list.Add((app, property));
                }
            }
        }

        var results = new List<RecurringProperty>();

        foreach (var (name, uses) in byName)
        {
            var slugs = uses.Select(u => u.App.Slug).Distinct(StringComparer.Ordinal).ToList();

            if (slugs.Count < 2)
                continue;

            var types = uses
                .Select(u => NormalizeType(u.Property.TypeName))
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var sizes = uses
                .Select(u => u.Property.Size)
                .Where(s => s.HasValue)
                .Select(s => s!.Value)
                .Distinct()
                .ToList();

            results.Add(new RecurringProperty
            {
                Name = name,
                TypeName = types.Count == 1 ? types[0] : null,
                ConflictingTypes = types.Count > 1 ? types : [],
                ScalarConflict = types.Count > 1 && types.Any(IsScalar),
                Size = sizes.Count == 1 ? sizes[0] : null,
                In = apps
                    .Where(a => slugs.Contains(a.Slug, StringComparer.Ordinal))
                    .Select(a => new CorpusSite
                    {
                        Application = a.Name,
                        Slug = a.Slug,
                        Weight = uses.Count(u => u.App.Slug == a.Slug),
                    })
                    .ToList(),
            });
        }

        // Scalar disagreements first, so the cap can never be what removes the one finding somebody
        // would have acted on.
        return results
            .OrderByDescending(r => r.ScalarConflict)
            .ThenByDescending(r => r.In.Count)
            .ThenByDescending(r => r.TotalUses)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ------------------------------------------------------------------
    // What they all depend on
    // ------------------------------------------------------------------

    private static List<SharedDependency> SharedDependencies(IReadOnlyList<WikiApplication> apps)
    {
        var results = new List<SharedDependency>();

        results.AddRange(Shared(
            apps,
            DependencyKind.RequiredModule,
            app => app.Project.ModuleInfo?.RequiredModules ?? []));

        results.AddRange(Shared(
            apps,
            DependencyKind.Package,
            app => app.Project.PackageReferences));

        return results
            .OrderByDescending(d => d.Universal)
            .ThenByDescending(d => d.Applications.Count)
            .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<SharedDependency> Shared(
        IReadOnlyList<WikiApplication> apps,
        DependencyKind kind,
        Func<WikiApplication, IEnumerable<string>> declared)
    {
        var byName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in apps)
        {
            foreach (var name in declared(app).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!byName.TryGetValue(name, out var list))
                    byName[name] = list = [];

                list.Add(app.Name);
            }
        }

        return byName
            .Where(pair => pair.Value.Count >= 2)
            .Select(pair => new SharedDependency
            {
                Name = pair.Key,
                Kind = kind,
                Applications = pair.Value,
                Universal = pair.Value.Count == apps.Count,
            })
            .ToList();
    }

    // ------------------------------------------------------------------

    private static List<ExtractedProperty> Declared(ExtractedEntity entity) =>
        entity.Properties.Where(p => p.InheritedFrom is null).ToList();

    /// <summary>
    /// One spelling per type, so two spellings of one type never read as two types.
    /// </summary>
    /// <remarks>
    /// The nullable mark is kept where it means something and dropped where it does not.
    /// <c>decimal?</c> beside <c>decimal</c> is a real difference in what the application allows;
    /// <c>Cliente?</c> beside <c>Cliente</c> is one file with nullable reference types switched on
    /// and another without, which says nothing about the model.
    /// </remarks>
    internal static string NormalizeType(string? typeName)
    {
        var text = (typeName ?? string.Empty).Trim();

        if (text.Length == 0)
            return string.Empty;

        var nullable = text.EndsWith('?');
        var core = (nullable ? text[..^1] : text).Trim();

        // A qualified name is the same type as its short form for every purpose here.
        var dot = core.LastIndexOf('.');
        if (dot >= 0 && dot < core.Length - 1 && core.IndexOf('<', StringComparison.Ordinal) < 0)
            core = core[(dot + 1)..];

        if (TypeAliases.TryGetValue(core, out var alias))
            core = alias;

        return nullable && ScalarValueTypes.Contains(core) ? core + "?" : core;
    }

    /// <summary>
    /// Whether a type is one of the shapes where two applications disagreeing is worth reporting.
    /// </summary>
    /// <remarks>
    /// A name meaning <c>decimal</c> here and <c>double</c> there is a defect waiting in an invoice
    /// total. A name meaning <c>XPCollection&lt;Cobro&gt;</c> here and
    /// <c>XPCollection&lt;CobroDetalle&gt;</c> there is two applications using an ordinary word for
    /// two different things, which is what words do. Only the first is a finding.
    /// </remarks>
    internal static bool IsScalar(string? normalizedType)
    {
        var core = (normalizedType ?? string.Empty).TrimEnd('?');

        return core == "string" || ScalarValueTypes.Contains(core);
    }

    private static string StripGenerics(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var angle = typeName.IndexOf('<', StringComparison.Ordinal);

        return (angle < 0 ? typeName : typeName[..angle]).Trim();
    }
}
