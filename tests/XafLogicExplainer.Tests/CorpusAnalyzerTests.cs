using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Core.Wiki;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That reading several applications together produces facts about the corpus, and only facts.
/// </summary>
/// <remarks>
/// The wiki exists to answer one question a developer with years of client work cannot answer from
/// memory: <em>have I built this before?</em> An answer that is merely plausible is worse than no
/// answer, because it sends someone to copy a class that does not exist. So every finding here has
/// to survive the same test: could a person disagree with it after opening the file?
/// </remarks>
public class CorpusAnalyzerTests
{
    // ---------------------------------------------------------------- classes modelled twice

    /// <summary>
    /// A class name two applications both model is the corpus finding worth having.
    /// </summary>
    [Fact]
    public void ClassModelledInTwoApplicationsIsRecurring()
    {
        var corpus = Analyze(
            App("Legal", Entity("Cliente", Prop("Nombre", "String"))),
            App("Presupuesto", Entity("Cliente", Prop("Nombre", "String"))));

        var recurring = Assert.Single(corpus.RecurringEntities);
        Assert.Equal("Cliente", recurring.ClassName);
        Assert.Equal(2, recurring.In.Count);
    }

    /// <summary>
    /// A class only one application models is not a corpus finding, however interesting it is.
    /// </summary>
    [Fact]
    public void ClassInOneApplicationIsNotRecurring()
    {
        var corpus = Analyze(
            App("Legal", Entity("Expediente", Prop("Numero", "String"))),
            App("Presupuesto", Entity("Partida", Prop("Monto", "Decimal"))));

        Assert.Empty(corpus.RecurringEntities);
    }

    /// <summary>
    /// The application that modelled it in most detail is named first, because that is the one to
    /// open before writing the class a third time.
    /// </summary>
    [Fact]
    public void RichestApplicationComesFirst()
    {
        var corpus = Analyze(
            App("Thin", Entity("Cliente", Prop("Nombre", "String"))),
            App("Rich", Entity("Cliente",
                Prop("Nombre", "String"),
                Prop("Rnc", "String"),
                Prop("Limite", "Decimal"))));

        var recurring = Assert.Single(corpus.RecurringEntities);
        Assert.Equal("Rich", recurring.Richest);
        Assert.Equal(3, recurring.In[0].Weight);
    }

    /// <summary>
    /// The comparison is the point: which application has the property the other one lacks.
    /// </summary>
    [Fact]
    public void PropertyComparisonNamesWhoDeclaresWhat()
    {
        var corpus = Analyze(
            App("Legal", Entity("Cliente", Prop("Nombre", "String"), Prop("Rnc", "String"))),
            App("Presupuesto", Entity("Cliente", Prop("Nombre", "String"))));

        var recurring = Assert.Single(corpus.RecurringEntities);

        var shared = recurring.Properties.Single(p => p.Name == "Nombre");
        Assert.Equal(2, shared.Applications.Count);

        var only = recurring.Properties.Single(p => p.Name == "Rnc");
        Assert.Equal("Legal", Assert.Single(only.Applications));

        Assert.False(recurring.Agrees);
    }

    /// <summary>
    /// Two applications that declare the same property names agree, and say so.
    /// </summary>
    [Fact]
    public void IdenticalShapesAgree()
    {
        var corpus = Analyze(
            App("A", Entity("Cliente", Prop("Nombre", "String"))),
            App("B", Entity("Cliente", Prop("Nombre", "String"))));

        Assert.True(Assert.Single(corpus.RecurringEntities).Agrees);
    }

    /// <summary>
    /// A property name declared as two different types in two applications has no agreed type.
    /// </summary>
    [Fact]
    public void DisagreeingTypeIsNotReportedAsAgreed()
    {
        var corpus = Analyze(
            App("A", Entity("Cliente", Prop("Codigo", "String"))),
            App("B", Entity("Cliente", Prop("Codigo", "Int32"))));

        var property = Assert.Single(corpus.RecurringEntities).Properties.Single(p => p.Name == "Codigo");
        Assert.Null(property.TypeName);
    }

    /// <summary>
    /// Inherited members are excluded from the comparison.
    /// </summary>
    /// <remarks>
    /// An entity carries what it inherits so that a reader of one entity is told the whole truth.
    /// Folding those into a corpus comparison would report two applications as agreeing on a class
    /// when all they share is <c>BaseObject</c>.
    /// </remarks>
    [Fact]
    public void InheritedPropertiesDoNotCountAsAgreement()
    {
        var inherited = Prop("Oid", "Guid");
        inherited.InheritedFrom = "BaseObject";

        var corpus = Analyze(
            App("A", Entity("Cliente", Prop("Nombre", "String"), inherited)),
            App("B", Entity("Cliente", Prop("Rnc", "String"), Clone(inherited))));

        var recurring = Assert.Single(corpus.RecurringEntities);
        Assert.DoesNotContain(recurring.Properties, p => p.Name == "Oid");
        Assert.False(recurring.Agrees);
    }

    // ---------------------------------------------------------------- your own layer

    /// <summary>
    /// A base class whose own source was read, reused in two applications, is this developer's layer.
    /// </summary>
    [Fact]
    public void BaseClassReadInTheCorpusAndReusedIsReported()
    {
        var baseClass = Entity("AuditedEntity", Prop("CreadoPor", "String"));

        var corpus = Analyze(
            App("A", baseClass, Derived("Cliente", "AuditedEntity"), Derived("Factura", "AuditedEntity")),
            App("B", Derived("Proveedor", "AuditedEntity")));

        var reused = Assert.Single(corpus.RecurringBaseTypes, b => b.Kind == BaseTypeKind.Entity);
        Assert.Equal("AuditedEntity", reused.Name);
        Assert.Equal(3, reused.TotalDerived);
        Assert.Equal("A", reused.DeclaredAt.Application);
    }

    /// <summary>
    /// A base class nobody in the corpus declares is the framework, and is never claimed as yours.
    /// </summary>
    /// <remarks>
    /// This is the whole reason the rule is "we read its source" rather than "its name is not on a
    /// list of DevExpress types": a list has to be maintained against every DevExpress release, and
    /// the day it falls behind the wiki starts telling a developer that <c>BaseObject</c> is theirs.
    /// </remarks>
    [Fact]
    public void FrameworkBaseIsNeverClaimedAsYours()
    {
        var corpus = Analyze(
            App("A", Derived("Cliente", "BaseObject")),
            App("B", Derived("Proveedor", "BaseObject")));

        Assert.Empty(corpus.RecurringBaseTypes);
    }

    /// <summary>
    /// A controller base that only says "this is a controller" carries no information.
    /// </summary>
    [Fact]
    public void GenericControllerBasesAreNotALayer()
    {
        var corpus = Analyze(
            App("A", [], [Controller("ClienteController", "ViewController")]),
            App("B", [], [Controller("FacturaController", "ViewController")]));

        Assert.Empty(corpus.RecurringBaseTypes);
    }

    /// <summary>
    /// A controller extending shipped DevExpress behaviour is the framework, not a layer of yours.
    /// </summary>
    [Fact]
    public void ControllerExtendingDevExpressBehaviourIsExcluded()
    {
        var a = Controller("MyDelete", "DeleteObjectsViewController");
        a.FrameworkBaseType = "DeleteObjectsViewController";

        var b = Controller("OtherDelete", "DeleteObjectsViewController");
        b.FrameworkBaseType = "DeleteObjectsViewController";

        var corpus = Analyze(
            App("A", [], [a]),
            App("B", [], [b]));

        Assert.Empty(corpus.RecurringBaseTypes);
    }

    /// <summary>
    /// A controller base written here and reused across applications is reported.
    /// </summary>
    [Fact]
    public void ControllerBaseWrittenHereIsReported()
    {
        var corpus = Analyze(
            App("A", [], [Controller("BaseAuditController", "ViewController"), Controller("ClienteController", "BaseAuditController")]),
            App("B", [], [Controller("FacturaController", "BaseAuditController")]));

        var reused = Assert.Single(corpus.RecurringBaseTypes);
        Assert.Equal("BaseAuditController", reused.Name);
        Assert.Equal(BaseTypeKind.Controller, reused.Kind);
    }

    // ---------------------------------------------------------------- actions written twice

    /// <summary>
    /// An action written in two applications is one idea implemented twice.
    /// </summary>
    [Fact]
    public void ActionDeclaredInTwoApplicationsIsRecurring()
    {
        var corpus = Analyze(
            App("A", [], [Controller("C1", "ViewController", Action("Aprobar", "Aprobar"))]),
            App("B", [], [Controller("C2", "ViewController", Action("Aprobar", "Aprobar"))]));

        var recurring = Assert.Single(corpus.RecurringActions);
        Assert.Equal("Aprobar", recurring.ActionId);
        Assert.Equal("Aprobar", recurring.Caption);
        Assert.Equal(2, recurring.In.Count);
    }

    /// <summary>
    /// The same action twice inside one application is that application repeating itself, which is
    /// a different finding and not this one.
    /// </summary>
    [Fact]
    public void ActionRepeatedInsideOneApplicationIsNotACorpusFinding()
    {
        var corpus = Analyze(
            App("A", [], [
                Controller("C1", "ViewController", Action("Aprobar", "Aprobar")),
                Controller("C2", "ViewController", Action("Aprobar", "Aprobar")),
            ]),
            App("B", [], [Controller("C3", "ViewController", Action("Cerrar", "Cerrar"))]));

        Assert.Empty(corpus.RecurringActions);
    }

    /// <summary>
    /// Two captions for one identifier is a disagreement, so no caption is claimed.
    /// </summary>
    [Fact]
    public void DisagreeingCaptionsAreNotResolved()
    {
        var corpus = Analyze(
            App("A", [], [Controller("C1", "ViewController", Action("Aprobar", "Aprobar"))]),
            App("B", [], [Controller("C2", "ViewController", Action("Aprobar", "Approve"))]));

        Assert.Null(Assert.Single(corpus.RecurringActions).Caption);
    }

    // ---------------------------------------------------------------- conventions

    /// <summary>
    /// A name used in two applications is a habit worth naming.
    /// </summary>
    [Fact]
    public void PropertyNameUsedAcrossApplicationsIsAConvention()
    {
        var corpus = Analyze(
            App("A", Entity("Cliente", Sized("Codigo", "String", 20))),
            App("B", Entity("Factura", Sized("Codigo", "String", 20))));

        var convention = Assert.Single(corpus.Conventions);
        Assert.Equal("Codigo", convention.Name);
        // Reported under one spelling, whichever the source used.
        Assert.Equal("string", convention.TypeName);
        Assert.Equal(20, convention.Size);
        Assert.True(convention.Consistent);
    }

    /// <summary>
    /// One name meaning two scalar shapes is the finding only a corpus can produce.
    /// </summary>
    [Fact]
    public void OneNameWithTwoShapesIsReportedAsInconsistent()
    {
        var corpus = Analyze(
            App("A", Entity("Cliente", Prop("Codigo", "string"))),
            App("B", Entity("Factura", Prop("Codigo", "int"))));

        var convention = Assert.Single(corpus.Conventions);
        Assert.False(convention.Consistent);
        Assert.True(convention.ScalarConflict);
        Assert.Equal(2, convention.ConflictingTypes.Count);
        Assert.Null(convention.TypeName);
    }

    /// <summary>
    /// <c>Double</c> and <c>double</c> are one type, and saying otherwise is a false accusation.
    /// </summary>
    /// <remarks>
    /// Both spellings turned up in one real corpus, on the same property name. A tool that reports
    /// that as a disagreement stops being believed about the disagreements that are real.
    /// </remarks>
    [Fact]
    public void TwoSpellingsOfOneTypeAreNotADisagreement()
    {
        var corpus = Analyze(
            App("A", Entity("Cliente", Prop("Cantidad", "Double"))),
            App("B", Entity("Factura", Prop("Cantidad", "double"))));

        var convention = Assert.Single(corpus.Conventions);
        Assert.True(convention.Consistent);
        Assert.Equal("double", convention.TypeName);
    }

    /// <summary>
    /// A nullable annotation on a reference type says nothing about the model.
    /// </summary>
    [Fact]
    public void NullableAnnotationOnAReferenceTypeIsNotADisagreement()
    {
        var corpus = Analyze(
            App("A", Entity("Contrato", Prop("Cliente", "Cliente"))),
            App("B", Entity("Factura", Prop("Cliente", "Cliente?"))));

        Assert.True(Assert.Single(corpus.Conventions).Consistent);
    }

    /// <summary>
    /// A nullable value type is a different decision about what the application allows.
    /// </summary>
    [Fact]
    public void NullableValueTypeIsADisagreement()
    {
        var corpus = Analyze(
            App("A", Entity("Factura", Prop("Total", "decimal"))),
            App("B", Entity("Cobro", Prop("Total", "decimal?"))));

        var convention = Assert.Single(corpus.Conventions);
        Assert.False(convention.Consistent);
        Assert.True(convention.ScalarConflict);
    }

    /// <summary>
    /// Two applications holding different things under an ordinary word are not disagreeing.
    /// </summary>
    /// <remarks>
    /// <c>Details</c> as a collection of invoice lines in one application and of reconciliation
    /// lines in another is what ordinary words do. Reporting it alongside <c>decimal</c> against
    /// <c>double</c> buries the one finding somebody would have acted on.
    /// </remarks>
    [Fact]
    public void DifferentCollectionsUnderOneNameAreNotAConflict()
    {
        var corpus = Analyze(
            App("A", Entity("Factura", Prop("Details", "XPCollection<FacturaDetalle>"))),
            App("B", Entity("Cobro", Prop("Details", "XPCollection<CobroDetalle>"))));

        var convention = Assert.Single(corpus.Conventions);
        Assert.False(convention.Consistent);
        Assert.False(convention.ScalarConflict);
    }

    /// <summary>
    /// One application modelling a relationship where another stores text is a real divergence.
    /// </summary>
    [Fact]
    public void ScalarAgainstEntityIsAConflictWorthReporting()
    {
        var corpus = Analyze(
            App("A", Entity("Factura", Prop("Cliente", "Cliente"))),
            App("B", Entity("Cobro", Prop("Cliente", "string"))));

        Assert.True(Assert.Single(corpus.Conventions).ScalarConflict);
    }

    /// <summary>
    /// A scalar disagreement is never the finding the cap removes.
    /// </summary>
    [Fact]
    public void ScalarDisagreementsSurviveTheCap()
    {
        var left = new List<ExtractedProperty> { Prop("Total", "decimal") };
        var right = new List<ExtractedProperty> { Prop("Total", "double") };

        for (var n = 0; n < 70; n++)
        {
            left.Add(Prop($"Campo{n:00}", "String"));
            right.Add(Prop($"Campo{n:00}", "String"));
        }

        var corpus = Analyze(
            App("A", Entity("Uno", [.. left])),
            App("B", Entity("Dos", [.. right])));

        Assert.Equal("Total", corpus.Conventions[0].Name);
        Assert.True(corpus.Conventions[0].ScalarConflict);
    }

    /// <summary>
    /// A capped list says how much it left out, so it never reads as a complete one.
    /// </summary>
    [Fact]
    public void CappedConventionListSaysWhatItLeftOut()
    {
        var left = new List<ExtractedProperty>();
        var right = new List<ExtractedProperty>();

        for (var n = 0; n < 70; n++)
        {
            left.Add(Prop($"Campo{n:00}", "String"));
            right.Add(Prop($"Campo{n:00}", "String"));
        }

        var corpus = Analyze(
            App("A", Entity("Uno", [.. left])),
            App("B", Entity("Dos", [.. right])));

        Assert.Equal(60, corpus.Conventions.Count);
        Assert.Equal(10, corpus.ConventionsNotShown);
    }

    // ---------------------------------------------------------------- shared dependencies

    /// <summary>
    /// A module every application requires is marked as such; one only some require is not.
    /// </summary>
    [Fact]
    public void UniversalDependencyIsDistinguishedFromACommonOne()
    {
        var a = App("A");
        a.Project.ModuleInfo = new ExtractedModuleInfo { RequiredModules = ["ValidationModule", "AuditTrailModule"] };

        var b = App("B");
        b.Project.ModuleInfo = new ExtractedModuleInfo { RequiredModules = ["ValidationModule"] };

        var c = App("C");
        c.Project.ModuleInfo = new ExtractedModuleInfo { RequiredModules = ["ValidationModule", "AuditTrailModule"] };

        var corpus = CorpusAnalyzer.Analyze([a, b, c]);

        var validation = corpus.SharedDependencies.Single(d => d.Name == "ValidationModule");
        Assert.True(validation.Universal);

        var audit = corpus.SharedDependencies.Single(d => d.Name == "AuditTrailModule");
        Assert.False(audit.Universal);
        Assert.Equal(2, audit.Applications.Count);
    }

    /// <summary>
    /// A dependency only one application declares is not shared.
    /// </summary>
    [Fact]
    public void DependencyInOneApplicationIsNotShared()
    {
        var a = App("A");
        a.Project.PackageReferences = ["Some.Private.Package"];

        var b = App("B");

        Assert.Empty(CorpusAnalyzer.Analyze([a, b]).SharedDependencies);
    }

    // ---------------------------------------------------------------- anchors

    /// <summary>
    /// Two applications that slug to the same string still get separate anchors.
    /// </summary>
    [Fact]
    public void SlugsAreUniqueWithinTheCorpus()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        Assert.Equal("pw-legal-office", CorpusAnalyzer.Slug("pw Legal Office", taken));
        Assert.Equal("pw-legal-office-2", CorpusAnalyzer.Slug("PW/Legal/Office", taken));
    }

    /// <summary>
    /// A name with nothing sluggable in it still produces an anchor.
    /// </summary>
    [Fact]
    public void NameWithNoUsableCharactersStillGetsAnAnchor()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal);

        Assert.Equal("app", CorpusAnalyzer.Slug("///", taken));
        Assert.Equal("app-2", CorpusAnalyzer.Slug("---", taken));
    }

    // ---------------------------------------------------------------- helpers

    // ------------------------------------------------- the scaffold everybody was given

    /// <summary>
    /// The XAF Project Wizard writes the same user classes into every solution, so two
    /// applications sharing them have not built anything twice.
    /// </summary>
    /// <remarks>
    /// The rule that lets them in is the right rule: a class is yours when its own source was
    /// read, and the wizard writes these into your tree. What separates them is not their name
    /// but the framework contract they implement -- so a developer who renamed
    /// <c>ApplicationUser</c> is caught too, and a future template with new names still is.
    /// </remarks>
    [Fact]
    public void WizardScaffoldIsNotCountedAsAClassModelledTwice()
    {
        var corpus = Analyze(
            App("Reportes", Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String"))),
            App("Lims", Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String"))));

        var recurring = Assert.Single(corpus.RecurringEntities);

        Assert.True(recurring.IsTemplate);
        Assert.Equal(0, corpus.ModelledTwiceCount);
    }

    /// <summary>
    /// It is still on the page, with the contract that earned it the label.
    /// </summary>
    /// <remarks>
    /// Excluding it from the count is a judgement; hiding it would be a second one nobody asked
    /// for. A reader who disagrees has to be able to see what was decided and check it.
    /// </remarks>
    [Fact]
    public void ScaffoldIsStillReportedAndNamesTheContractItCarries()
    {
        var corpus = Analyze(
            App("Reportes", Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String"))),
            App("Lims", Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String"))));

        var template = Assert.Single(corpus.Templates);

        Assert.Equal("ApplicationUser", template.ClassName);
        Assert.Equal(["ISecurityUserWithLoginInfo"], template.Contracts);
    }

    /// <summary>
    /// A user class the developer extended is a finding again, because the shapes disagree.
    /// </summary>
    /// <remarks>
    /// Somebody who added the same two properties to the scaffold in two applications really has
    /// modelled something, and that difference is what the property comparison exists to show.
    /// The contract alone must never be enough to dismiss a class.
    /// </remarks>
    [Fact]
    public void ASecurityUserTheDeveloperExtendedIsAFindingAgain()
    {
        var corpus = Analyze(
            App("Reportes", Secured("ApplicationUser", "ISecurityUserWithLoginInfo",
                Prop("UserName", "String"), Prop("Departamento", "String"))),
            App("Lims", Secured("ApplicationUser", "ISecurityUserWithLoginInfo",
                Prop("UserName", "String"))));

        var recurring = Assert.Single(corpus.RecurringEntities);

        Assert.False(recurring.IsTemplate);
        Assert.Equal(1, corpus.ModelledTwiceCount);
    }

    /// <summary>
    /// An ordinary class two applications model is untouched by any of this.
    /// </summary>
    [Fact]
    public void AClassCarryingNoContractIsCountedAsBefore()
    {
        var corpus = Analyze(
            App("Legal", Entity("Cliente", Prop("Nombre", "String"))),
            App("Presupuesto", Entity("Cliente", Prop("Nombre", "String"))));

        var recurring = Assert.Single(corpus.RecurringEntities);

        Assert.False(recurring.IsTemplate);
        Assert.Equal(1, corpus.ModelledTwiceCount);
    }

    /// <summary>
    /// The scaffold does not make two unrelated applications look alike on the overlap grid.
    /// </summary>
    /// <remarks>
    /// The count beside "classes modelled twice" is not the only number the scaffold inflates:
    /// the grid answers <em>which two of my projects are most alike?</em> from the class names
    /// they share, and it reads the entities directly rather than the recurring list.
    /// </remarks>
    [Fact]
    public void ScaffoldDoesNotMakeTwoApplicationsOverlapOnTheGrid()
    {
        var corpus = Analyze(
            App("Reportes",
                Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String")),
                Entity("Programacion", Prop("Cron", "String"))),
            App("Lims",
                Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String")),
                Entity("Muestra", Prop("Codigo", "String"))));

        var grid = CorpusGraph.Overlap(corpus);
        var pair = grid.Cells.First(c => !c.IsSelf);

        Assert.Equal(0, pair.Shared);
    }

    /// <summary>
    /// Findings come first; the scaffold is context and sorts last.
    /// </summary>
    [Fact]
    public void TemplatesSortAfterEveryRealFinding()
    {
        var corpus = Analyze(
            App("Reportes",
                Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String")),
                Entity("Cliente", Prop("Nombre", "String"))),
            App("Lims",
                Secured("ApplicationUser", "ISecurityUserWithLoginInfo", Prop("UserName", "String")),
                Entity("Cliente", Prop("Nombre", "String"))));

        Assert.Equal(["Cliente", "ApplicationUser"], corpus.RecurringEntities.Select(r => r.ClassName));
    }

    private static WikiCorpus Analyze(params WikiApplication[] applications) =>
        CorpusAnalyzer.Analyze(applications);

    private static WikiApplication App(string name, params ExtractedEntity[] entities) =>
        App(name, entities, []);

    private static WikiApplication App(
        string name,
        IEnumerable<ExtractedEntity> entities,
        IEnumerable<ExtractedController> controllers) =>
        new()
        {
            Name = name,
            Slug = name.ToLowerInvariant(),
            Project = new ExtractedProject
            {
                ProjectName = name,
                ProjectPath = Path.Combine(Path.GetTempPath(), name),
                Entities = [.. entities],
                Controllers = [.. controllers],
            },
        };

    private static ExtractedEntity Entity(string className, params ExtractedProperty[] properties) =>
        new()
        {
            ClassName = className,
            Namespace = "Fixture.Module.BusinessObjects",
            BaseType = "BaseObject",
            FilePath = Path.Combine(Path.GetTempPath(), "App", $"{className}.cs"),
            Line = 10,
            Properties = [.. properties],
        };

    private static ExtractedEntity Derived(string className, string baseType)
    {
        var entity = Entity(className);
        entity.BaseType = baseType;
        return entity;
    }

    private static ExtractedController Controller(
        string className,
        string baseType,
        params ExtractedAction[] actions) =>
        new()
        {
            ClassName = className,
            Namespace = "Fixture.Module.Controllers",
            BaseControllerType = baseType,
            FilePath = Path.Combine(Path.GetTempPath(), "App", $"{className}.cs"),
            Line = 12,
            Actions = [.. actions],
        };

    private static ExtractedAction Action(string actionId, string caption) =>
        new()
        {
            ActionId = actionId,
            Caption = caption,
            ActionType = "SimpleAction",
            FilePath = Path.Combine(Path.GetTempPath(), "App", "Controller.cs"),
            Line = 20,
        };

    private static ExtractedEntity Secured(
        string className,
        string contract,
        params ExtractedProperty[] properties)
    {
        var entity = Entity(className, properties);

        // The base list as extraction reads it: the class, then the contract it implements. The
        // wizard writes `: PermissionPolicyUser, ISecurityUserWithLoginInfo`.
        entity.BaseType = "PermissionPolicyUser";
        entity.BaseTypes = ["PermissionPolicyUser", contract];

        return entity;
    }

    private static ExtractedProperty Prop(string name, string typeName) =>
        new() { Name = name, TypeName = typeName };

    private static ExtractedProperty Sized(string name, string typeName, int size) =>
        new() { Name = name, TypeName = typeName, Size = size };

    private static ExtractedProperty Clone(ExtractedProperty property) => property.Clone();
}
