using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// Several XAF applications read together, plus what they turn out to have in common.
/// </summary>
/// <remarks>
/// Everything on this type is <em>computed</em> from extraction. Nothing here is a claim a person
/// wrote down about the corpus, because a hand-written claim about ten applications is a claim
/// nobody re-checks after the eleventh arrives.
/// </remarks>
public sealed class WikiCorpus
{
    /// <summary>
    /// The applications, in the order they were given.
    /// </summary>
    public required IReadOnlyList<WikiApplication> Applications { get; init; }

    /// <summary>
    /// Classes modelled under the same name in more than one application.
    /// </summary>
    public IReadOnlyList<RecurringEntity> RecurringEntities { get; init; } = [];

    /// <summary>
    /// The recurring classes that are a finding: everything the framework did not supply.
    /// </summary>
    /// <remarks>
    /// Every count on the page reads from here rather than from <see cref="RecurringEntities"/>,
    /// so a scaffold two applications both received cannot be reported as something their author
    /// built twice. The templates stay on the page, said out loud, under their own heading.
    /// </remarks>
    public IEnumerable<RecurringEntity> ModelledTwice => RecurringEntities.Where(r => !r.IsTemplate);

    /// <summary>
    /// How many classes were modelled more than once, framework templates excluded.
    /// </summary>
    public int ModelledTwiceCount => RecurringEntities.Count(r => !r.IsTemplate);

    /// <summary>
    /// The recurring classes the framework supplied rather than the author.
    /// </summary>
    public IEnumerable<RecurringEntity> Templates => RecurringEntities.Where(r => r.IsTemplate);

    /// <summary>
    /// Base classes written here and then reused across applications.
    /// </summary>
    public IReadOnlyList<RecurringBaseType> RecurringBaseTypes { get; init; } = [];

    /// <summary>
    /// Actions declared under the same identifier in more than one application.
    /// </summary>
    public IReadOnlyList<RecurringAction> RecurringActions { get; init; } = [];

    /// <summary>
    /// Property names used in more than one application, and whether the shape agrees.
    /// </summary>
    public IReadOnlyList<RecurringProperty> Conventions { get; init; } = [];

    /// <summary>
    /// Modules and packages more than one application depends on.
    /// </summary>
    public IReadOnlyList<SharedDependency> SharedDependencies { get; init; } = [];

    /// <summary>
    /// Conventions found but not listed, so a capped list never reads as a complete one.
    /// </summary>
    public int ConventionsNotShown { get; init; }
}

/// <summary>
/// One application as the wiki refers to it.
/// </summary>
public sealed class WikiApplication
{
    /// <summary>
    /// The name to show: the configured profile name when there is one, else the project name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// A stable identifier for anchors and for search filtering.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// What extraction read.
    /// </summary>
    public required ExtractedProject Project { get; init; }

    /// <summary>
    /// Actions across every controller.
    /// </summary>
    public int ActionCount => Project.Controllers.Sum(c => c.Actions.Count);
}

/// <summary>
/// Where something was found, and in which application.
/// </summary>
public sealed class CorpusSite
{
    /// <summary>
    /// The display name of the application.
    /// </summary>
    public required string Application { get; init; }

    /// <summary>
    /// The anchor slug of the application.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// A source citation, relative to the project.
    /// </summary>
    public string? Citation { get; init; }

    /// <summary>
    /// The class, controller, or entity the thing was found on.
    /// </summary>
    public string? Owner { get; init; }

    /// <summary>
    /// A count that means something for the kind of finding: properties, subclasses, uses.
    /// </summary>
    public int Weight { get; init; }
}

/// <summary>
/// A class name modelled in more than one application.
/// </summary>
public sealed class RecurringEntity
{
    /// <summary>
    /// The class name, which is what recurs. Namespaces usually differ.
    /// </summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Each application that models it, richest first.
    /// </summary>
    public required IReadOnlyList<CorpusSite> In { get; init; }

    /// <summary>
    /// Every property name any of them declares, and who declares it.
    /// </summary>
    public IReadOnlyList<PropertyPresence> Properties { get; init; } = [];

    /// <summary>
    /// The application that models it in the most detail.
    /// </summary>
    public string Richest => In.Count == 0 ? string.Empty : In[0].Application;

    /// <summary>
    /// True when every application declares exactly the same property names.
    /// </summary>
    public bool Agrees => Properties.All(p => p.Applications.Count == In.Count);

    /// <summary>
    /// True when every application declares this class with a DevExpress security contract and
    /// with the same properties -- the framework user type, not a class modelled here.
    /// </summary>
    /// <remarks>
    /// Both halves are load-bearing. The contract alone would call a developer own security user
    /// template code; agreement alone would call any two identical classes framework. Together
    /// they claim only what was read: this carries a framework contract, and nobody here changed
    /// it. Extend it in two applications and it becomes a finding again, which is right -- that
    /// difference is exactly what the property comparison exists to show.
    /// </remarks>
    public bool IsTemplate { get; init; }

    /// <summary>
    /// The DevExpress security contracts this class carries, if any.
    /// </summary>
    public IReadOnlyList<string> Contracts { get; init; } = [];
}

/// <summary>
/// One property of a recurring class, and which applications declare it.
/// </summary>
public sealed class PropertyPresence
{
    /// <summary>
    /// The property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The applications that declare it.
    /// </summary>
    public required IReadOnlyList<string> Applications { get; init; }

    /// <summary>
    /// The declared type where every application agrees, otherwise null.
    /// </summary>
    public string? TypeName { get; init; }
}

/// <summary>
/// A base class written here and reused in more than one application.
/// </summary>
public sealed class RecurringBaseType
{
    /// <summary>
    /// The base class name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether it sits under entities or under controllers.
    /// </summary>
    public required BaseTypeKind Kind { get; init; }

    /// <summary>
    /// Each application that derives from it, and how many classes do.
    /// </summary>
    public required IReadOnlyList<CorpusSite> In { get; init; }

    /// <summary>
    /// Classes deriving from it across the whole corpus.
    /// </summary>
    public int TotalDerived => In.Sum(s => s.Weight);

    /// <summary>
    /// Where the base class itself was read.
    /// </summary>
    /// <remarks>
    /// A base type earns a place here only when its own source was read in one of the applications.
    /// That is what separates a layer this developer wrote from one XAF ships: no list of DevExpress
    /// type names is involved, so nothing here rots when DevExpress renames something.
    /// </remarks>
    public required CorpusSite DeclaredAt { get; init; }
}

/// <summary>
/// What a reused base class sits under.
/// </summary>
public enum BaseTypeKind
{
    /// <summary>A persistent class.</summary>
    Entity,

    /// <summary>A controller.</summary>
    Controller,
}

/// <summary>
/// An action identifier declared in more than one application.
/// </summary>
public sealed class RecurringAction
{
    /// <summary>
    /// The action identifier.
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// The caption where every application agrees, otherwise null.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>
    /// Each application that declares it.
    /// </summary>
    public required IReadOnlyList<CorpusSite> In { get; init; }
}

/// <summary>
/// A property name used across applications, and whether its shape agrees.
/// </summary>
public sealed class RecurringProperty
{
    /// <summary>
    /// The property name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The declared type where every use agrees, otherwise null.
    /// </summary>
    public string? TypeName { get; init; }

    /// <summary>
    /// The declared types when they disagree, in the order first seen.
    /// </summary>
    public IReadOnlyList<string> ConflictingTypes { get; init; } = [];

    /// <summary>
    /// The size where every use that declares one agrees, otherwise null.
    /// </summary>
    public int? Size { get; init; }

    /// <summary>
    /// Each application that uses the name, and on how many classes.
    /// </summary>
    public required IReadOnlyList<CorpusSite> In { get; init; }

    /// <summary>
    /// Classes carrying the name across the whole corpus.
    /// </summary>
    public int TotalUses => In.Sum(s => s.Weight);

    /// <summary>
    /// True when the name means one shape everywhere it appears.
    /// </summary>
    public bool Consistent => ConflictingTypes.Count == 0;

    /// <summary>
    /// True when the disagreement involves a scalar, which is the kind worth reporting.
    /// </summary>
    /// <remarks>
    /// <c>Total</c> declared <c>decimal</c> in one application and <c>double</c> in another is a
    /// defect waiting inside an invoice. <c>Details</c> declared <c>XPCollection&lt;Cobro&gt;</c> in
    /// one and <c>XPCollection&lt;FacturaDetalle&gt;</c> in another is two applications using an
    /// ordinary word for two different things, which is what ordinary words do. Only the first is
    /// worth a reader's attention, and mixing them buries it.
    /// </remarks>
    public bool ScalarConflict { get; init; }
}

/// <summary>
/// A module or package more than one application depends on.
/// </summary>
public sealed class SharedDependency
{
    /// <summary>
    /// The module type name or package identifier.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether it was declared as a required module or as a package reference.
    /// </summary>
    public required DependencyKind Kind { get; init; }

    /// <summary>
    /// The applications that declare it.
    /// </summary>
    public required IReadOnlyList<string> Applications { get; init; }

    /// <summary>
    /// True when every application in the corpus declares it.
    /// </summary>
    public bool Universal { get; init; }
}

/// <summary>
/// How a dependency was declared.
/// </summary>
public enum DependencyKind
{
    /// <summary>Named in the RequiredModuleTypes of the module.</summary>
    RequiredModule,

    /// <summary>A PackageReference in the project file.</summary>
    Package,
}
