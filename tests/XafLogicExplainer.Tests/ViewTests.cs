using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The screen inventory, and what XAF loads onto each screen.
/// </summary>
/// <remarks>
/// The answer exists in no file. XAF generates a list, detail and lookup view for every business
/// class and a list view for every collection, and a controller's presence on one of them is four
/// conditions the framework ands together at run time.
/// </remarks>
public class ViewTests
{
    [Theory]
    [InlineData("Prescription_ListView")]
    [InlineData("Prescription_DetailView")]
    [InlineData("Prescription_LookupListView")]
    public void GeneratesTheThreeViewsEveryClassGets(string id) =>
        Assert.Contains(SampleProjects.Demo.Views, view => view.Id == id);

    [Fact]
    public void GeneratesANestedListViewPerCollection()
    {
        // Patient.Prescriptions is shown by a view whose id belongs to the *owner*, not to the
        // class it lists -- which is why searching for "Prescription" never finds it.
        var view = View("Patient_Prescriptions_ListView");

        Assert.Equal("Prescription", view.ObjectType);
        Assert.Equal("Patient", view.OwnerEntity);
        Assert.Equal("Prescriptions", view.OwnerProperty);
        Assert.Equal(ViewNesting.Nested, view.Nesting);
    }

    [Fact]
    public void CountsEveryScreenTheApplicationHas()
    {
        // Fourteen classes at three views each, plus one per collection property. Pinned because
        // the number is the claim: an inventory that quietly drops a kind of view reads as a
        // complete list and is not one.
        var demo = SampleProjects.Demo;
        var collections = demo.Entities.Sum(e => e.Properties.Count(p => p.IsCollection));

        Assert.Equal((demo.Entities.Count * 3) + collections, demo.Views.Count);
        Assert.Equal(54, demo.Views.Count);
    }

    [Fact]
    public void KnowsWhichScreensNavigationOpens() =>
        Assert.True(View("Prescription_ListView").InNavigation);

    [Fact]
    public void PutsAControllerOnTheScreensItTargets()
    {
        // DispenseController is ViewController<DetailView> with TargetObjectType = Prescription,
        // so both conditions have to hold.
        Assert.Contains(View("Prescription_DetailView").Activates, a => a.Controller == "DispenseController");
        Assert.DoesNotContain(View("Prescription_ListView").Activates, a => a.Controller == "DispenseController");
        Assert.DoesNotContain(View("Patient_DetailView").Activates, a => a.Controller == "DispenseController");
    }

    [Fact]
    public void SaysWhyAControllerRunsThere()
    {
        var activation = View("Prescription_DetailView").Activates
            .Single(a => a.Controller == "DispenseController");

        Assert.Contains(activation.Reasons, r =>
            r.Condition == ActivationCondition.ViewType && r.Required == "DetailView");
        Assert.Contains(activation.Reasons, r =>
            r.Condition == ActivationCondition.ObjectType && r.Required == "Prescription");
        Assert.Contains(activation.Actions, action => action.StartsWith("DispensePrescription"));

        // The reasons are data, so the Spanish document does not end up with English inside it.
        Assert.Equal("la vista es un DetailView", ActivationReasonText.Spanish(activation.Reasons[0]));
        Assert.Equal("view is a DetailView", ActivationReasonText.English(activation.Reasons[0]));
    }

    [Fact]
    public void FindsTheControllerThatRunsOnEveryDetailView()
    {
        // The finding this whole section exists for. CustomizeExpiryEditorController names no
        // object type, so it is loaded onto the detail view of all fourteen classes -- something
        // no file in the repository states, and its own comment does not suggest.
        var everywhere = SampleProjects.Demo.Views
            .Where(v => v.Activates.Any(a => a.Controller == "CustomizeExpiryEditorController"))
            .ToList();

        Assert.Equal(
            SampleProjects.Demo.Views.Count(v => v.ViewType == ModelViewType.DetailView),
            everywhere.Count);

        Assert.All(everywhere, view => Assert.Equal(ModelViewType.DetailView, view.ViewType));
    }

    [Fact]
    public void ReadsAssignabilityRatherThanEquality()
    {
        // XAF asks targetObjectType.IsAssignableFrom(view's type), so targeting a base class
        // reaches every class under it. Reading it as equality would under-report the logic on a
        // screen, which is the direction that misleads.
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Sale" },
            new() { Id = "Patient_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Patient" },
        };

        var controller = new ExtractedController
        {
            ClassName = "AuditController",
            Targeting = new ControllerTargeting { TargetObjectType = "AuditedBase" },
        };

        var entities = new List<ExtractedEntity>
        {
            new() { ClassName = "Sale", BaseType = "AuditedBase" },
            new() { ClassName = "Patient", BaseType = "BaseObject" },
        };

        ViewActivationResolver.Resolve(views, [controller], entities);

        Assert.Single(views[0].Activates);
        Assert.Equal("Sale is a AuditedBase", ActivationReasonText.English(views[0].Activates[0].Reasons.Single()));
        Assert.Empty(views[1].Activates);
    }

    [Fact]
    public void CountsAnObjectViewAsBothListAndDetail()
    {
        // ListView and DetailView both derive from ObjectView, which is what several built-in
        // controllers target -- and a dashboard is neither.
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_ListView", ViewType = ModelViewType.ListView, ObjectType = "Sale" },
            new() { Id = "Sale_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Sale" },
            new() { Id = "Overview", ViewType = ModelViewType.DashboardView },
        };

        var controller = new ExtractedController
        {
            ClassName = "AnyObjectViewController",
            Targeting = new ControllerTargeting { TypeOfView = "ObjectView" },
        };

        ViewActivationResolver.Resolve(views, [controller], [new ExtractedEntity { ClassName = "Sale" }]);

        Assert.Single(views[0].Activates);
        Assert.Single(views[1].Activates);
        Assert.Empty(views[2].Activates);
    }

    [Fact]
    public void KeepsANestedControllerOffRootScreens()
    {
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_ListView", ViewType = ModelViewType.ListView, Nesting = ViewNesting.Root },
            new() { Id = "Order_Lines_ListView", ViewType = ModelViewType.ListView, Nesting = ViewNesting.Nested },
        };

        var controller = new ExtractedController
        {
            ClassName = "NestedOnlyController",
            Targeting = new ControllerTargeting { Nesting = "Nested" },
        };

        ViewActivationResolver.Resolve(views, [controller], []);

        Assert.Empty(views[0].Activates);
        Assert.Single(views[1].Activates);
    }

    [Fact]
    public void ListsAnUnreadableTargetSeparatelyRatherThanEverywhere()
    {
        // Putting a controller whose TargetViewId cannot be read onto every screen would invent an
        // appearance on all of them. It belongs on none, and named apart so it is not lost.
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Sale" },
        };

        var controller = new ExtractedController
        {
            ClassName = "DialogController",
            Targeting = new ControllerTargeting { UnresolvedViewId = "Module.DialogViewId" },
        };

        ViewActivationResolver.Resolve(views, [controller], []);

        Assert.Empty(views[0].Activates);
        Assert.Equal("DialogController", ViewActivationResolver.Undetermined([controller]).Single().ClassName);
    }

    [Fact]
    public void ReadsAnActionsOwnTargeting()
    {
        // An action carries its own copy of the four conditions and can be narrower than the
        // controller that owns it.
        var project = SampleProjects.Xpo;
        var controller = project.Controllers.Single(c => c.ClassName == "ApproveOrderController");

        Assert.All(controller.Actions, action => Assert.NotNull(action.Targeting));
    }

    private static ExtractedView View(string id) =>
        SampleProjects.Demo.Views.FirstOrDefault(v => v.Id == id)
        ?? throw new InvalidOperationException(
            $"The demo has no view '{id}'. Extracted: " +
            string.Join(", ", SampleProjects.Demo.Views.Select(v => v.Id)));
}
