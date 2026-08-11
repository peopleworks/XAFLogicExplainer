using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The cases where the tool used to state more than it knew.
/// </summary>
/// <remarks>
/// Every one of these came out of an audit rather than a bug report, and every one produced a
/// confident sentence in generated documentation. They share a shape: an input the reader did not
/// understand, quietly treated as an input that restricted nothing — which is the one direction the
/// cardinal rule forbids, because "runs on every screen" reads as knowledge.
/// </remarks>
public class OverReportingTests
{
    [Theory]
    [InlineData("public C() => TargetViewType = ViewType.ListView;", "ListView")]
    [InlineData("public C() { base.TargetViewType = ViewType.ListView; }", "ListView")]
    [InlineData("void InitializeComponent() { TargetViewType = ViewType.ListView; }", "ListView")]
    public void ReadsTargetingWhereverTheControllerSetsItUp(string member, string expected)
    {
        // An expression-bodied constructor, an assignment through `base`, and the designer's
        // InitializeComponent -- the last is where every migrated XAF application keeps its
        // targeting, and all three used to come out as "restricts nothing".
        var targeting = Read($"class C : ViewController {{ {member} }}");

        Assert.Equal(expected, targeting.TypeOfView);
        Assert.False(targeting.IsUnrestricted);
    }

    [Fact]
    public void DoesNotClaimAnActionsTargetingSetInAnInitializer()
    {
        // The initializer form is the one DevExpress documentation uses. Only the member-access
        // form was guarded, so this attributed the action's restriction to the whole controller --
        // wrong in both directions at once.
        var targeting = Read("""
            class C : ViewController {
                public C() {
                    var merge = new SimpleAction(this, "Merge", "Edit") {
                        TargetViewId = "Merge_DetailView", TargetObjectType = typeof(Order) };
                }
            }
            """);

        Assert.True(targeting.IsUnrestricted);
    }

    [Theory]
    [InlineData("TargetViewType = isList ? ViewType.ListView : ViewType.DetailView;")]
    [InlineData("TargetViewType = viewType;")]
    [InlineData("TargetViewNesting = nested ? Nesting.Nested : Nesting.Root;")]
    [InlineData("TargetObjectType = XafTypesInfo.Instance.FindTypeInfo(name).Type;")]
    public void RecordsAConditionItCannotReadInsteadOfIgnoringIt(string assignment)
    {
        // The ternary was the worst: taking the text after the last dot of the whole expression
        // reported a confident restriction to DetailView. The others silently vanished, which
        // reads as no restriction at all.
        var targeting = Read($"class C : ViewController {{ public C() {{ {assignment} }} }}");

        Assert.False(targeting.IsUnrestricted);
        Assert.True(targeting.IsUndetermined);
        Assert.NotEmpty(targeting.Unreadable);
    }

    [Fact]
    public void KeepsAnUnreadableControllerOffEveryScreen()
    {
        var views = new List<ExtractedView>
        {
            new() { Id = "Order_ListView", ViewType = ModelViewType.ListView, ObjectType = "Order" },
        };

        var controller = new ExtractedController
        {
            ClassName = "MysteryController",
            Targeting = new ControllerTargeting { Unreadable = { "TargetViewType = pickOne()" } },
        };

        ViewActivationResolver.Resolve(views, [controller], []);

        Assert.Empty(views[0].Activates);
        Assert.Contains(ViewActivationResolver.Undetermined([controller]), c => c.ClassName == "MysteryController");
    }

    [Fact]
    public void DropsAControllerThatARegisteredDescendantReplaces()
    {
        // XAF activates only the most derived controller of a chain: registering a descendant
        // evicts its base. Listing both duplicated every action the base provides, and credited
        // shipped behaviour to the framework in the one application that replaced it.
        var project = new ExtractedProject
        {
            Controllers =
            {
                new ExtractedController { ClassName = "ArchiveController", BaseControllerType = "DeleteObjectsViewController" },
            },
            Views =
            {
                new ExtractedView
                {
                    Id = "Order_ListView",
                    ViewType = ModelViewType.ListView,
                    ObjectType = "Order",
                    Activates =
                    {
                        new ViewActivation { Controller = "ArchiveController" },
                        new ViewActivation { Controller = "DeleteObjectsViewController", Framework = true },
                    },
                },
            },
            FrameworkAlwaysActive = { "DeleteObjectsViewController" },
        };

        ViewActivationResolver.SuppressReplacedControllers(project, Catalog());

        var activation = Assert.Single(project.Views[0].Activates);

        Assert.Equal("ArchiveController", activation.Controller);
        Assert.Equal(["DeleteObjectsViewController"], activation.Replaces);
        Assert.Empty(project.FrameworkAlwaysActive);
    }

    [Fact]
    public void KeepsAWindowControllerOffEveryScreen()
    {
        // It has none of the four view conditions, so "unrestricted" put it on every screen.
        var project = new ExtractedProject
        {
            Controllers =
            {
                new ExtractedController { ClassName = "AboutController", BaseControllerType = "WindowController" },
            },
            Views =
            {
                new ExtractedView { Id = "Order_ListView", ViewType = ModelViewType.ListView, ObjectType = "Order" },
            },
        };

        ControllerTargetingResolver.Resolve(project.Controllers, null);
        ViewActivationResolver.Resolve(project.Views, project.Controllers, project.Entities);

        Assert.True(project.Controllers[0].IsWindowController);
        Assert.Empty(project.Views[0].Activates);
    }

    [Theory]
    [InlineData("Winery.Module", false)]
    [InlineData("Darwin.Core", false)]
    [InlineData("Shop.Win", true)]
    [InlineData("Shop.Win.Server", true)]
    public void MatchesAPlatformProjectByWholeSegment(string projectName, bool expected)
    {
        // `Contains("Win")` pulled every WinForms controller onto a Blazor application's screens.
        var project = new ExtractedProject
        {
            Controllers = { new ExtractedController { ClassName = "Own", SourceProject = projectName } },
        };

        var assemblies = FrameworkModuleScope.Resolve(project, new XafCatalog());

        Assert.Equal(expected, assemblies.Contains("DevExpress.ExpressApp.Win"));
    }

    [Fact]
    public void DoesNotInventANestedViewForACollectionOfNonEntities()
    {
        // XAF generates a nested list view only when the collection holds a business class. A
        // List<string> gets none, and inventing the id puts framework controllers on a screen that
        // does not exist.
        var project = new ExtractedProject
        {
            Entities =
            {
                new ExtractedEntity
                {
                    ClassName = "Order",
                    Properties =
                    {
                        new ExtractedProperty { Name = "Tags", TypeName = "IList<string>", IsCollection = true },
                        new ExtractedProperty { Name = "Lines", TypeName = "XPCollection<OrderLine>", IsCollection = true },
                    },
                },
                new ExtractedEntity { ClassName = "OrderLine" },
            },
        };

        var views = ViewInventory.Build(project).Select(view => view.Id).ToList();

        Assert.Contains("Order_Lines_ListView", views);
        Assert.DoesNotContain("Order_Tags_ListView", views);
    }

    private static ControllerTargeting Read(string source) =>
        ControllerTargetingReader.Read(CSharpSyntaxTree.ParseText(source).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().First());

    private static XafCatalog Catalog() => new()
    {
        Controllers =
        {
            ["DeleteObjectsViewController"] = new XafCatalogType
            {
                Name = "DeleteObjectsViewController",
                Assembly = "DevExpress.ExpressApp",
                BaseType = "ViewController",
            },
        },
    };
}
