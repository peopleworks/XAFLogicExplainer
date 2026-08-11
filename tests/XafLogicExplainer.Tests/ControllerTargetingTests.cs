using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Where XAF activates a controller.
/// </summary>
/// <remarks>
/// <c>ViewController.IsFitToView</c> ands four conditions together — nesting, view type, object type
/// and view id — and every one of them is unrestricted when unset. Reading one wrong in the
/// permissive direction says a controller runs on every screen in the application when it runs on
/// one, which is a worse answer than none.
/// </remarks>
public class ControllerTargetingTests
{
    [Fact]
    public void ReadsTheViewFromASingleGenericArgument()
    {
        // ViewController<DetailView> constrains the view, not the object. It was being skipped
        // entirely: the reader only looked at generic bases with two arguments, so every
        // ViewController<DetailView> in an application reported no view type at all.
        var targeting = Read("class C : ViewController<DetailView> { }");

        Assert.Equal("DetailView", targeting.TypeOfView);
        Assert.Null(targeting.TargetObjectType);
    }

    [Fact]
    public void ReadsBothArgumentsOfAnObjectViewController()
    {
        var targeting = Read("class C : ObjectViewController<ListView, Order> { }");

        Assert.Equal("ListView", targeting.TypeOfView);
        Assert.Equal("Order", targeting.TargetObjectType);
    }

    [Theory]
    [InlineData("TargetViewType = ViewType.ListView;")]
    [InlineData("this.TargetViewType = DevExpress.ExpressApp.ViewType.ListView;")]
    [InlineData("TypeOfView = typeof(ListView);")]
    [InlineData("this.TypeOfView = typeof(DevExpress.ExpressApp.ListView);")]
    public void ReadsEverySpellingOfTheViewType(string assignment) =>
        Assert.Equal("ListView", Read($"class C : ViewController {{ public C() {{ {assignment} }} }}").TypeOfView);

    [Theory]
    [InlineData("TargetObjectType = typeof(Order);", "Order")]
    [InlineData("this.TargetObjectType = typeof(SampleApp.Module.BusinessObjects.Order);", "Order")]
    public void ReadsTheObjectTypeAssignedInTheConstructor(string assignment, string expected) =>
        Assert.Equal(expected, Read($"class C : ViewController {{ public C() {{ {assignment} }} }}").TargetObjectType);

    [Fact]
    public void PrefersTheConstructorOverTheGenericBase()
    {
        // Constructors run base-first, so the explicit assignment is what survives.
        var targeting = Read(
            "class C : ObjectViewController<DetailView, Order> { public C() { TargetObjectType = typeof(Invoice); } }");

        Assert.Equal("Invoice", targeting.TargetObjectType);
        Assert.Equal("DetailView", targeting.TypeOfView);
    }

    [Theory]
    [InlineData("TargetViewType = ViewType.Any;")]
    [InlineData("TypeOfView = typeof(View);")]
    public void TreatsAnyAsNoRestrictionAtAll(string assignment)
    {
        var targeting = Read($"class C : ViewController<DetailView> {{ public C() {{ {assignment} }} }}");

        Assert.Null(targeting.TypeOfView);
        Assert.True(targeting.IsUnrestricted);
    }

    [Fact]
    public void ReadsNesting() =>
        Assert.Equal("Nested",
            Read("class C : ViewController { public C() { TargetViewNesting = Nesting.Nested; } }").Nesting);

    [Fact]
    public void SplitsTheSemicolonSeparatedViewIdList()
    {
        // XAF accepts a list in the single TargetViewId string, and IsFitToViewID compares the whole
        // string first and then each part. Treating the whole string as one id matches no view.
        var targeting = Read(
            "class C : ViewController { public C() { TargetViewId = \"Order_DetailView;Invoice_DetailView\"; } }");

        Assert.Contains("Order_DetailView", targeting.ViewIds);
        Assert.Contains("Invoice_DetailView", targeting.ViewIds);
    }

    [Fact]
    public void DoesNotTrimTheViewIdListBecauseXafDoesNot()
    {
        // ViewController.IsFitToViewID splits on ';' with no trimming, so a space after the
        // separator makes that entry match nothing and the controller silently never activates
        // there. Trimming here reported it on a screen it never reaches, and hid the typo.
        var targeting = Read(
            "class C : ViewController { public C() { TargetViewId = \"Order_DetailView; Invoice_DetailView\"; } }");

        Assert.Contains("Order_DetailView", targeting.ViewIds);
        Assert.DoesNotContain("Invoice_DetailView", targeting.ViewIds);
        Assert.Contains(" Invoice_DetailView", targeting.ViewIds);
    }

    [Fact]
    public void ResolvesAViewIdHeldInAConstant()
    {
        var targeting = Read("""
            class C : ViewController {
                public const string DialogViewId = "Merge_DetailView";
                public C() { TargetViewId = DialogViewId; }
            }
            """);

        Assert.Equal(["Merge_DetailView"], targeting.ViewIds);
    }

    [Fact]
    public void KeepsAViewIdItCannotResolve()
    {
        // Naming the expression is the honest answer. A controller restricted to a view id we
        // cannot read is still restricted, and reporting no restriction would be a false claim
        // about the application.
        var targeting = Read(
            "class C : ViewController { public C() { TargetViewId = DashboardsModule.DetailViewName; } }");

        Assert.Empty(targeting.ViewIds);
        Assert.Equal("DashboardsModule.DetailViewName", targeting.UnresolvedViewId);
        Assert.False(targeting.IsUnrestricted);
    }

    [Theory]
    [InlineData("TargetViewId = \"\";")]
    [InlineData("TargetViewId = string.Empty;")]
    [InlineData("TargetViewId = ActionBase.AnyCaption;")]
    public void TreatsAnEmptyViewIdAsEveryView(string assignment) =>
        Assert.True(Read($"class C : ViewController {{ public C() {{ {assignment} }} }}").IsUnrestricted);

    [Fact]
    public void IgnoresTargetingThatBelongsToAnAction()
    {
        // An action carries its own copy of the same four properties, which XAF evaluates
        // separately to narrow the action inside an already-active controller. Attributing it to
        // the controller would hide the controller from every other view it really runs on.
        var targeting = Read("""
            class C : ViewController {
                public C() {
                    var mergeAction = new SimpleAction(this, "Merge", "Edit");
                    mergeAction.TargetViewId = "Merge_DetailView";
                    mergeAction.TargetObjectType = typeof(Order);
                }
            }
            """);

        Assert.True(targeting.IsUnrestricted);
    }

    [Fact]
    public void IgnoresAGenericParameterAsATarget()
    {
        // class Reusable<T> : ViewController<T> targets nothing in particular; only the classes
        // that close the generic do.
        var targeting = Read("class Reusable<T> : ViewController<T> where T : View { }");

        Assert.True(targeting.IsUnrestricted);
    }

    [Fact]
    public void InheritsTargetingFromTheControllerItExtends()
    {
        // Targeting is set in a constructor, and base constructors run first. Reading each class
        // on its own reported every controller whose base does the targeting as running on every
        // view in the application.
        var controllers = Controllers("""
            class ListControllerBase : ViewController {
                public ListControllerBase() { TargetViewType = ViewType.ListView; TargetViewNesting = Nesting.Root; }
            }
            class MyController : ListControllerBase { }
            """);

        ControllerTargetingResolver.Resolve(controllers, null);

        var derived = controllers.Single(c => c.ClassName == "MyController").Targeting;

        Assert.Equal("ListView", derived.TypeOfView);
        Assert.Equal("Root", derived.Nesting);
    }

    [Fact]
    public void PrefersItsOwnTargetingOverTheOneItInherits()
    {
        var controllers = Controllers("""
            class ListControllerBase : ViewController {
                public ListControllerBase() { TargetViewType = ViewType.ListView; }
            }
            class MyController : ListControllerBase {
                public MyController() { TargetViewType = ViewType.DetailView; TargetObjectType = typeof(Order); }
            }
            """);

        ControllerTargetingResolver.Resolve(controllers, null);

        var derived = controllers.Single(c => c.ClassName == "MyController").Targeting;

        Assert.Equal("DetailView", derived.TypeOfView);
        Assert.Equal("Order", derived.TargetObjectType);
    }

    [Fact]
    public void InheritsTargetingFromAFrameworkController()
    {
        // The base is not in this project and its constructor is not in this source tree, so the
        // catalog is the only thing that knows DeleteObjectsViewController runs on object views.
        var catalog = new XafCatalog
        {
            Controllers =
            {
                ["DeleteObjectsViewController"] = new XafCatalogType
                {
                    Name = "DeleteObjectsViewController",
                    Targeting = new ControllerTargeting { TypeOfView = "ObjectView" },
                    TargetingSource = "sources",
                },
            },
        };

        var controllers = Controllers("class MyDeleteController : DeleteObjectsViewController { }");

        ControllerTargetingResolver.Resolve(controllers, catalog);

        Assert.Equal("ObjectView", controllers[0].Targeting.TypeOfView);
    }

    [Fact]
    public void TerminatesOnACycleInTheBaseChain()
    {
        // Source that does not compile is the situation this analysis is built for, and a mutual
        // base pair is one of the shapes a half-finished refactor leaves behind.
        var controllers = Controllers("""
            class A : B { }
            class B : A { }
            """);

        ControllerTargetingResolver.Resolve(controllers, null);

        Assert.All(controllers, controller => Assert.True(controller.Targeting.IsUnrestricted));
    }

    [Fact]
    public void ReportsTheDemoControllerOnBothConditions()
    {
        // DispenseController is written the way XAF documentation writes them --
        // ViewController<DetailView> plus TargetObjectType in the constructor -- and the view half
        // of that was silently dropped.
        var controller = SampleProjects.Demo.Controllers.Single(c => c.ClassName == "DispenseController");

        Assert.Equal("Prescription", controller.Targeting.TargetObjectType);
        Assert.Equal("DetailView", controller.Targeting.TypeOfView);
    }

    private static ControllerTargeting Read(string source) =>
        ControllerTargetingReader.Read(Classes(source).First());

    private static IEnumerable<ClassDeclarationSyntax> Classes(string source) =>
        CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();

    /// <summary>
    /// Builds extracted controllers from source, the way the analyzer does, for the resolver tests.
    /// </summary>
    private static List<ExtractedController> Controllers(string source) =>
    [
        .. Classes(source).Select(classDecl => new ExtractedController
        {
            ClassName = classDecl.Identifier.Text,
            BaseControllerType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? "object",
            Targeting = ControllerTargetingReader.Read(classDecl),
        }),
    ];
}
