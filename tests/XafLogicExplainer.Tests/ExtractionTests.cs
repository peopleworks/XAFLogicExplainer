using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Reading an XAF application's business model from source.
/// </summary>
public class ExtractionTests
{
    [Fact]
    public void FindsEveryEntity()
    {
        var names = SampleProjects.Xpo.Entities.Select(e => e.ClassName).OrderBy(n => n).ToList();

        Assert.Equal(["Customer", "Order", "OrderLine"], names);
    }

    [Fact]
    public void ReadsProjectMetadataFromTheCsproj()
    {
        Assert.Equal("net10.0", SampleProjects.Xpo.TargetFramework);
        Assert.Contains(SampleProjects.Xpo.PackageReferences, p => p.Contains("DevExpress.ExpressApp.Xpo"));
    }

    [Fact]
    public void ResolvesNameOfInTheDefaultPropertyAttribute()
    {
        // The regression: this reported the literal text "nameof(Name)" as a property name.
        Assert.Equal("Name", SampleProjects.Xpo.Entity("Customer").DefaultProperty);
        Assert.Equal("Number", SampleProjects.Xpo.Entity("Order").DefaultProperty);
    }

    [Fact]
    public void ReadsPropertyTypesAndSizes()
    {
        var name = SampleProjects.Xpo.Entity("Customer").Property("Name");

        Assert.Equal("string", name.TypeName);
        Assert.Equal(120, name.Size);
    }

    [Fact]
    public void ReadsDescriptions()
    {
        var customer = SampleProjects.Xpo.Entity("Customer");

        Assert.Equal("A company that places orders.", customer.Description);
        Assert.Equal("Blocked customers cannot receive new orders.", customer.Property("IsBlocked").Description);
    }

    [Fact]
    public void RecognizesCalculatedProperties()
    {
        var total = SampleProjects.Xpo.Entity("Order").Property("Total");

        Assert.Equal("Lines.Sum(LineTotal)", total.PersistentAlias);
    }

    [Fact]
    public void RecordsLookupFilters()
    {
        var customer = SampleProjects.Xpo.Entity("Order").Property("Customer");

        Assert.Equal("IsBlocked = False", customer.DataSourceCriteria);
        Assert.True(customer.ImmediatePostData);
    }

    [Fact]
    public void ReadsAssociationsInBothDirections()
    {
        var customerToOrders = SampleProjects.Xpo.Entity("Customer").Relationships
            .Single(r => r.RelatedEntity == "Order");
        var orderToCustomer = SampleProjects.Xpo.Entity("Order").Relationships
            .Single(r => r.RelatedEntity == "Customer");

        Assert.Equal("Customer-Orders", customerToOrders.AssociationName);
        Assert.Equal(RelationshipType.OneToMany, customerToOrders.Type);
        Assert.Equal(RelationshipType.ManyToOne, orderToCustomer.Type);
    }

    [Fact]
    public void MarksAggregatedRelationshipsAsOwned()
    {
        var lines = SampleProjects.Xpo.Entity("Order").Relationships
            .Single(r => r.RelatedEntity == "OrderLine");

        Assert.True(lines.IsAggregated);
    }

    [Fact]
    public void ReadsValidationRulesWithTheirMessages()
    {
        var rules = SampleProjects.Xpo.Entity("Customer").ValidationRules;

        var required = Assert.Single(rules, r => r.RuleType.Contains("Required"));
        Assert.Equal("Name", required.TargetProperty);
        Assert.Equal("A customer must have a name.", required.MessageTemplate);
    }

    [Fact]
    public void ReadsCriteriaBasedValidation()
    {
        var criteria = Assert.Single(
            SampleProjects.Xpo.Entity("Order").ValidationRules,
            r => r.RuleType.Contains("Criteria"));

        Assert.Equal("An order total cannot be negative.", criteria.MessageTemplate);
    }

    [Fact]
    public void ReadsAppearanceRules()
    {
        var appearance = Assert.Single(SampleProjects.Xpo.Entity("Order").AppearanceRules);

        Assert.Equal("OrderLockedWhenApproved", appearance.Id);
        Assert.Equal("IsApproved", appearance.Criteria);
        Assert.Equal("DetailView", appearance.Context);
    }

    [Fact]
    public void ReadsNavigationGroups()
    {
        var sales = Assert.Single(SampleProjects.Xpo.Navigation, n => n.GroupName == "Sales");

        Assert.Contains("Customer", sales.EntityClassNames);
        Assert.Contains("Order", sales.EntityClassNames);
        Assert.DoesNotContain("OrderLine", sales.EntityClassNames);
    }

    [Fact]
    public void FindsControllersAndTheirTargets()
    {
        var controller = Assert.Single(SampleProjects.Xpo.Controllers);

        Assert.Equal("ApproveOrderController", controller.ClassName);
        Assert.Contains("Order", controller.TargetObjectType);
    }

    [Fact]
    public void ReadsActionsWithTheirConfiguration()
    {
        var action = Assert.Single(SampleProjects.Xpo.Controllers.Single().Actions);

        Assert.Equal("ApproveOrder", action.ActionId);
        Assert.Equal("Approve", action.Caption);
        Assert.Contains("cannot be edited afterwards", action.ConfirmationMessage);
    }

    [Fact]
    public void CapturesTheCodeAnActionRuns()
    {
        var action = SampleProjects.Xpo.Controllers.Single().Actions.Single();

        // The handler body is what answers "what does this button actually do", so losing it
        // makes the controller detail useless.
        Assert.NotNull(action.ExecuteMethodBody);
        Assert.Contains("IsApproved = true", action.ExecuteMethodBody);
        Assert.Contains("blocked customer", action.ExecuteMethodBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindsSeedData()
    {
        var seed = Assert.Single(SampleProjects.Xpo.SeedData);

        Assert.Equal("CreateDefaultCustomers", seed.MethodName);
        Assert.Contains("Walk-in", seed.RawSourceCode);
    }

    [Fact]
    public void ReadsModelEditorCustomizations()
    {
        var model = SampleProjects.Xpo.ModelEditorInfo;

        Assert.NotNull(model);
        Assert.Equal("Sample Sales", model!.ApplicationTitle);

        var customer = Assert.Single(model.BOModelClasses, c => c.ClassName == "Customer");
        Assert.Equal("Client", customer.Caption);
        Assert.True(customer.IsCloneable);

        Assert.Contains(model.Views, v => v.Id == "Order_ListView");
    }

    [Fact]
    public void ProducesAStableSourceFingerprint()
    {
        // Change detection depends on this being deterministic across runs of the same source.
        Assert.False(string.IsNullOrWhiteSpace(SampleProjects.Xpo.SourceHash));
    }
}
