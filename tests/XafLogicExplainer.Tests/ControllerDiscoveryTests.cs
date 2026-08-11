using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Which classes count as controllers, through the real extraction pipeline.
/// </summary>
/// <remarks>
/// Deliberately not built from hand-made <see cref="ExtractedController"/> lists. Extraction used
/// to return the <em>first</em> controller class per file, and to recognise only classes deriving
/// directly from <c>ViewController</c>, <c>ObjectViewController</c> or <c>WindowController</c> —
/// so a controller extending a base class or a DevExpress one was dropped from the output
/// entirely. Every unit test still passed, because they all constructed their own inputs.
/// <para>
/// A controller that is never seen cannot be reported as missing, which makes this the one gap the
/// tool's own "absence is an answer" promise cannot survive.
/// </para>
/// </remarks>
public class ControllerDiscoveryTests
{
    [Theory]
    [InlineData("BulkApproveController")]
    [InlineData("OrderExportController")]
    [InlineData("OrderListControllerBase")]
    public void FindsEveryControllerDeclaredInOneFile(string className) =>
        Assert.Equal(className, SampleProjects.Xpo.Controller(className).ClassName);

    [Fact]
    public void FindsAControllerThatExtendsAnApplicationBaseClass()
    {
        // Neither of these states any targeting of its own; the base class does, in its
        // constructor, and constructors run base-first.
        var controller = SampleProjects.Xpo.Controller("BulkApproveController");

        Assert.Equal("Order", controller.Targeting.TargetObjectType);
        Assert.Equal("ListView", controller.Targeting.TypeOfView);
    }

    [Fact]
    public void MarksAnAbstractBaseAsOneThatNeverRuns()
    {
        Assert.True(SampleProjects.Xpo.Controller("OrderListControllerBase").IsAbstract);
        Assert.False(SampleProjects.Xpo.Controller("BulkApproveController").IsAbstract);
    }

    [Fact]
    public void KeepsAnAbstractBaseOffEveryScreen()
    {
        // It targets Order list views, so it fits one — and XAF can never instantiate it.
        Assert.DoesNotContain(
            SampleProjects.Xpo.Views.SelectMany(view => view.Activates),
            activation => activation.Controller == "OrderListControllerBase");

        Assert.Contains(
            SampleProjects.Xpo.Views.Single(view => view.Id == "Order_ListView").Activates,
            activation => activation.Controller == "BulkApproveController");
    }

    [Fact]
    public void FindsAControllerThatExtendsADevExpressOne()
    {
        // `ArchiveInsteadOfDeleteController : DeleteObjectsViewController` is the example the
        // README advertises as what the catalog makes possible. It was never extracted at all, so
        // the feature could not fire and nothing said so.
        var project = ExtractWithCatalog();
        var controller = project.Controller("ArchiveInsteadOfDeleteController");

        Assert.Equal("DeleteObjectsViewController", controller.FrameworkBaseType);

        // And it inherits that controller's targeting, which exists in no line of this codebase.
        Assert.Equal("ObjectView", controller.Targeting.TypeOfView);
    }

    [Fact]
    public void SaysItCannotPlaceTheDevExpressDescendantWithoutACatalog()
    {
        // Honest degradation. The class is still found — it is named for what it is and its file
        // imports XAF — but with no catalog there is nothing that could say what
        // `DeleteObjectsViewController` restricts, and targeting is inherited.
        //
        // "Unknown" is not "unrestricted". Reporting it as unrestricted is how a controller that
        // runs on object views comes to be claimed on every screen in the application, on the
        // strength of having no information about it at all.
        var controller = SampleProjects.Xpo.Controller("ArchiveInsteadOfDeleteController");

        Assert.Equal("DeleteObjectsViewController", controller.Targeting.UnresolvedBase);
        Assert.False(controller.Targeting.IsUnrestricted);

        Assert.DoesNotContain(
            SampleProjects.Xpo.Views.SelectMany(view => view.Activates),
            activation => activation.Controller == "ArchiveInsteadOfDeleteController");

        Assert.Contains(
            ViewActivationResolver.Undetermined(SampleProjects.Xpo.Controllers),
            undetermined => undetermined.ClassName == "ArchiveInsteadOfDeleteController");
    }

    /// <summary>
    /// Extracts the XPO fixture against a catalog holding one DevExpress controller.
    /// </summary>
    private static ExtractedProject ExtractWithCatalog()
    {
        var catalog = new XafCatalog
        {
            DevExpressVersion = "26.1",
            Controllers =
            {
                ["DeleteObjectsViewController"] = new XafCatalogType
                {
                    Name = "DeleteObjectsViewController",
                    Namespace = "DevExpress.ExpressApp.SystemModule",
                    Assembly = "DevExpress.ExpressApp",
                    BaseType = "ViewController",
                    Summary = "Contains the Delete Action.",
                    Targeting = new ControllerTargeting { TypeOfView = "ObjectView" },
                    TargetingSource = "sources",
                },
            },
        };

        return new LogicExtractor().ExtractFromSourceDirectory(SampleProjects.XpoPath, new ExtractionOptions
        {
            BaseTypeNames = ["XPCustomObject", "BaseObject", "XPObject", "XPLiteObject"],
            IncludeMethodBodies = true,
            LanguageCode = "en",
            UseCatalog = false,
            Catalog = catalog,
        });
    }
}
