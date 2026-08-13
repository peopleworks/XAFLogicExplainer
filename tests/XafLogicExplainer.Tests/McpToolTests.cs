using XafLogicExplainer.Mcp;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The tools an MCP client calls.
/// </summary>
/// <remarks>
/// Exercised through the same types the server registers, so the behavior under test is what a
/// connected agent actually receives.
/// </remarks>
public class McpToolTests
{
    private static XafProjectContext Context => new(
    [
        new XafProjectSource { Name = "SampleApp", Path = SampleProjects.XpoPath, Language = "en" },
        new XafProjectSource { Name = "SampleEf", Path = SampleProjects.EfCorePath, Language = "en" },
    ]);

    private static XafDiscoveryTools Discovery => new(Context);

    private static XafDetailTools Detail => new(Context);

    [Fact]
    public async Task OverviewReportsTheApplicationAndItsContents()
    {
        var result = await Discovery.OverviewAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("SampleApp", result);
        Assert.Contains("XPO", result);
        Assert.Contains("Customer", result);
        Assert.Contains("ApproveOrderController", result);
        Assert.Contains("complete", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OverviewSelectsAProjectByName()
    {
        var result = await Discovery.OverviewAsync("SampleEf", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Product", result);
        Assert.DoesNotContain("OrderLine", result);
    }

    [Fact]
    public async Task SearchFindsPropertiesAcrossEntities()
    {
        var result = await Discovery.SearchAsync("IsBlocked", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Customer.IsBlocked", result);
    }

    [Fact]
    public async Task SearchFindsActionsByTheirCode()
    {
        var result = await Discovery.SearchAsync("blocked customer", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("ApproveOrder", result);
    }

    [Fact]
    public async Task SearchNarrowsByKind()
    {
        var result = await Discovery.SearchAsync("Order", kind: "entity", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("**entity**", result);
        Assert.DoesNotContain("**controller**", result);
    }

    [Fact]
    public async Task SearchSaysPlainlyWhenSomethingIsAbsent()
    {
        var result = await Discovery.SearchAsync("Cryptocurrency", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("No match", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not appear", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EntityReturnsPropertiesRelationshipsAndRules()
    {
        var result = await Detail.EntityAsync("Order", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Number", result);
        Assert.Contains("OrderLine", result);
        Assert.Contains("Lines.Sum(LineTotal)", result);
        Assert.Contains("aggregated", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be negative", result);
    }

    [Fact]
    public async Task EntityLookupIgnoresCase()
    {
        var result = await Detail.EntityAsync("cUsToMeR", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("# Customer", result);
    }

    [Fact]
    public async Task AbsentEntityReturnsTheCompleteInventoryAndSaysItDoesNotExist()
    {
        // A bare "not found" invites an agent to assume it looked in the wrong place and invent
        // the type. Extraction covers the whole tree, so absence is a fact and is stated as one.
        var result = await Detail.EntityAsync("PurchaseOrder", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("There is no entity called 'PurchaseOrder'", result);
        Assert.Contains("complete list of 5 entities", result);
        Assert.Contains("Customer", result);
        Assert.Contains("has not been created yet", result);
    }

    [Fact]
    public async Task AbsentEntityUsesTheRightPluralForOne()
    {
        var single = new XafDetailTools(new XafProjectContext(
            [new XafProjectSource { Name = "SampleEf", Path = SampleProjects.EfCorePath, Language = "en" }]));

        var result = await single.EntityAsync("Nothing", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("2 entities", result);
        Assert.DoesNotContain("entitys", result);
    }

    [Fact]
    public async Task ControllerReturnsTheCodeAnActionRuns()
    {
        var result = await Detail.ControllerAsync("ApproveOrderController", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("ApproveOrder", result);
        Assert.Contains("IsApproved = true", result);
        Assert.Contains("```csharp", result);
    }

    [Fact]
    public async Task ControllerReportsWhatItTargets()
    {
        var result = await Detail.ControllerAsync("ApproveOrderController", cancellationToken: TestContext.Current.CancellationToken);

        // The generic argument of ViewController<DetailView> is a view type, not a business type;
        // reporting it as the target said something false about the application.
        Assert.Contains("Order", result);
        Assert.DoesNotContain("Applies to: DetailView", result);
    }

    [Fact]
    public async Task RulesCanBeNarrowedToOneEntity()
    {
        var result = await Detail.RulesAsync("Customer", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("A customer must have a name.", result);
        Assert.DoesNotContain("cannot be negative", result);
    }

    [Fact]
    public async Task RulesIncludeAppearanceAndCalculations()
    {
        var result = await Detail.RulesAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("OrderLockedWhenApproved", result);
        Assert.Contains("Quantity * UnitPrice", result);
    }

    [Fact]
    public async Task ModelReturnsCustomizationsThatExistOnlyInXml()
    {
        var result = await Detail.ModelAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Sample Sales", result);
        Assert.Contains("Client", result);
        Assert.Contains("Order_ListView", result);
    }

    [Fact]
    public async Task ModelSaysSoWhenThereAreNoCustomizations()
    {
        var result = await Detail.ModelAsync("SampleEf", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("no Model Editor customizations", result);
    }

    [Fact]
    public async Task RefreshReportsWhatItDiscarded()
    {
        var context = Context;
        var tools = new XafDiscoveryTools(context);

        await tools.OverviewAsync(cancellationToken: TestContext.Current.CancellationToken);
        var result = await tools.RefreshAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("1", result);
    }

    [Fact]
    public async Task AnUnknownProjectNameListsTheConfiguredOnes()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Discovery.OverviewAsync("NoSuchProject", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("SampleApp", error.Message);
        Assert.Contains("SampleEf", error.Message);
    }

    [Fact]
    public async Task RepeatedQueriesReuseTheParsedProject()
    {
        var context = Context;
        var tools = new XafDetailTools(context);

        var first = await tools.EntityAsync("Customer", cancellationToken: TestContext.Current.CancellationToken);
        var second = await tools.EntityAsync("Customer", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
    }
}
