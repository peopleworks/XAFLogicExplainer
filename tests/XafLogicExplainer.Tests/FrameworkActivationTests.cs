using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The framework's own controllers on an application's screens.
/// </summary>
/// <remarks>
/// Exercised against a synthetic catalog, so the suite still needs no DevExpress installation and
/// gives the same answer on every machine.
/// <para>
/// The rules are not symmetric with an application's own controllers, and each asymmetry is here
/// because the naive version reported something untrue: controllers from modules the application
/// never registered, controllers XAF cannot instantiate, and window controllers filed among the
/// ones that run on every view.
/// </para>
/// </remarks>
public class FrameworkActivationTests
{
    [Fact]
    public void PutsAFrameworkControllerOnTheViewsItTargets()
    {
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        var listView = project.Views.Single(v => v.Id == "Order_ListView");
        var detailView = project.Views.Single(v => v.Id == "Order_DetailView");

        Assert.Contains(listView.Activates, a => a.Controller == "FilterController");
        Assert.DoesNotContain(detailView.Activates, a => a.Controller == "FilterController");

        // ListView and DetailView both derive from ObjectView.
        Assert.Contains(listView.Activates, a => a.Controller == "DeleteObjectsViewController");
        Assert.Contains(detailView.Activates, a => a.Controller == "DeleteObjectsViewController");
    }

    [Fact]
    public void MarksThemAsTheFrameworksRatherThanTheTeams()
    {
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        var activation = project.Views
            .Single(v => v.Id == "Order_ListView").Activates
            .Single(a => a.Controller == "FilterController");

        Assert.True(activation.Framework);
        Assert.Equal("DevExpress.ExpressApp", activation.SourceProject);
        Assert.Equal("Filters a List View.", activation.Summary);
    }

    [Fact]
    public void LeavesOutModulesTheApplicationNeverRegistered()
    {
        // The Win controller is real, and it is not in a Blazor application. Reporting it would be
        // a statement about a screen that cannot happen.
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        Assert.DoesNotContain(
            project.Views.SelectMany(v => v.Activates),
            a => a.Controller == "WinFilterController");

        Assert.DoesNotContain("WinFilterController", project.FrameworkAlwaysActive);
    }

    [Fact]
    public void IncludesThePlatformModuleNobodyWroteDown()
    {
        // The platform module is registered by the application builder, not by the module class, so
        // it appears in no source this extraction reads. The Blazor project beside the module is
        // the evidence.
        var assemblies = FrameworkModuleScope.Resolve(Project(), Catalog());

        Assert.Contains("DevExpress.ExpressApp.Blazor", assemblies);
        Assert.Contains("DevExpress.ExpressApp", assemblies);
        Assert.Contains("DevExpress.ExpressApp.Validation", assemblies);
        Assert.DoesNotContain("DevExpress.ExpressApp.Win", assemblies);
    }

    [Theory]
    [InlineData("AbstractViewControllerBase")]
    [InlineData("GenericController`1")]
    [InlineData("ObsoleteViewController")]
    public void LeavesOutWhatXafWouldNeverInstantiate(string controller)
    {
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        Assert.DoesNotContain(project.Views.SelectMany(v => v.Activates), a => a.Controller == controller);
        Assert.DoesNotContain(controller, project.FrameworkAlwaysActive);
    }

    [Fact]
    public void LeavesOutWindowControllers()
    {
        // A WindowController belongs to a window, not to a view, and has none of the four view
        // conditions -- so its targeting reads as "unrestricted" and it would otherwise be filed
        // among the controllers that run on every screen.
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        Assert.DoesNotContain("AboutInfoController", project.FrameworkAlwaysActive);
        Assert.DoesNotContain(project.Views.SelectMany(v => v.Activates), a => a.Controller == "AboutInfoController");
    }

    [Fact]
    public void RecordsTheOnesThatRunEverywhereOnceInsteadOfOnEveryView()
    {
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        Assert.Contains("RefreshController", project.FrameworkAlwaysActive);
        Assert.DoesNotContain(project.Views.SelectMany(v => v.Activates), a => a.Controller == "RefreshController");
    }

    [Fact]
    public void SaysNothingWhereTheCatalogKnowsNothing()
    {
        // Null targeting means the DevExpress sources were absent when the catalog was built. It is
        // not "unrestricted", and treating it as such would put a controller on every screen on the
        // strength of having no information about it.
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, Catalog());

        Assert.DoesNotContain("UnknownTargetingController", project.FrameworkAlwaysActive);
        Assert.DoesNotContain(
            project.Views.SelectMany(v => v.Activates),
            a => a.Controller == "UnknownTargetingController");
    }

    [Fact]
    public void AddsNothingAtAllWithoutACatalog()
    {
        var project = Project();

        ViewActivationResolver.ResolveFramework(project, null);

        Assert.Empty(project.FrameworkAlwaysActive);
        Assert.Empty(project.Views.SelectMany(v => v.Activates));
    }

    /// <summary>
    /// An application with a Blazor platform project, registering Validation.
    /// </summary>
    private static ExtractedProject Project() => new()
    {
        ProjectName = "Sample",
        Entities = { new ExtractedEntity { ClassName = "Order" } },
        Controllers = { new ExtractedController { ClassName = "Own", SourceProject = "Sample.Blazor.Server" } },
        ModuleInfo = new ExtractedModuleInfo
        {
            RequiredModules = { "DevExpress.ExpressApp.Validation.ValidationModule" },
        },
        Views =
        {
            new ExtractedView
            {
                Id = "Order_ListView",
                ViewType = ModelViewType.ListView,
                ObjectType = "Order",
                Nesting = ViewNesting.Root,
            },
            new ExtractedView
            {
                Id = "Order_DetailView",
                ViewType = ModelViewType.DetailView,
                ObjectType = "Order",
                Nesting = ViewNesting.Either,
            },
        },
    };

    private static XafCatalog Catalog() => new()
    {
        DevExpressVersion = "26.1",
        Controllers =
        {
            ["ViewController"] = Entry("ViewController", "DevExpress.ExpressApp", baseType: "Controller"),
            ["WindowController"] = Entry("WindowController", "DevExpress.ExpressApp", baseType: "Controller"),

            ["FilterController"] = Entry("FilterController", "DevExpress.ExpressApp",
                typeOfView: "ListView", summary: "Filters a List View."),

            ["DeleteObjectsViewController"] = Entry("DeleteObjectsViewController", "DevExpress.ExpressApp",
                typeOfView: "ObjectView"),

            ["RefreshController"] = Entry("RefreshController", "DevExpress.ExpressApp"),

            // Registered module, so it counts.
            ["PersistenceValidationController"] = Entry("PersistenceValidationController",
                "DevExpress.ExpressApp.Validation", typeOfView: "ObjectView"),

            // Never registered by this application.
            ["WinFilterController"] = Entry("WinFilterController", "DevExpress.ExpressApp.Win",
                typeOfView: "ListView"),

            ["AbstractViewControllerBase"] = Entry("AbstractViewControllerBase", "DevExpress.ExpressApp",
                typeOfView: "ListView", isAbstract: true),
            ["GenericController`1"] = Entry("GenericController`1", "DevExpress.ExpressApp",
                typeOfView: "ListView"),
            ["ObsoleteViewController"] = Entry("ObsoleteViewController", "DevExpress.ExpressApp",
                typeOfView: "ListView", isObsolete: true),

            ["AboutInfoController"] = Entry("AboutInfoController", "DevExpress.ExpressApp",
                baseType: "WindowController"),

            ["UnknownTargetingController"] = new XafCatalogType
            {
                Name = "UnknownTargetingController",
                Assembly = "DevExpress.ExpressApp",
                BaseType = "ViewController",
            },
        },
        Modules =
        {
            ["ValidationModule"] = new XafCatalogType
            {
                Name = "ValidationModule",
                Assembly = "DevExpress.ExpressApp.Validation",
            },
        },
    };

    private static XafCatalogType Entry(
        string name,
        string assembly,
        string baseType = "ViewController",
        string? typeOfView = null,
        string? summary = null,
        bool isAbstract = false,
        bool isObsolete = false) => new()
    {
        Name = name,
        Assembly = assembly,
        BaseType = baseType,
        IsAbstract = isAbstract,
        IsObsolete = isObsolete,
        Summary = summary,
        Targeting = new ControllerTargeting { TypeOfView = typeOfView },
        TargetingSource = "sources",
    };
}
