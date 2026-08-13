namespace XafLogicExplainer.Tests;

/// <summary>
/// Entities that reach a persistent base through another class in the same project.
/// </summary>
/// <remarks>
/// Matching a class's own base list against a list of root names stops one hop short, and an
/// application that writes a shared base — auditing, a key convention, a display-name property —
/// puts every one of its business objects out of reach. It is the same defect the controller side
/// fixed by repeating until a round changes nothing.
/// <para>
/// Under EF Core the DbSet roster hides this: a registered class is found whatever it derives
/// from. XPO has no such fallback, and an EF Core class that is not registered has none either.
/// </para>
/// </remarks>
public class DeepInheritanceTests
{
    [Fact]
    public void FindsAnEntityTwoHopsFromAPersistentRoot()
    {
        var names = SampleProjects.DeepXpo.Entities.Select(entity => entity.ClassName);

        Assert.Contains("Order", names);
    }

    [Fact]
    public void FindsAnEntityThreeHopsFromAPersistentRoot()
    {
        // Only resolvable after Order has been accepted, which is what makes this a fixed point
        // rather than one more hop.
        var names = SampleProjects.DeepXpo.Entities.Select(entity => entity.ClassName);

        Assert.Contains("PriorityOrder", names);
    }

    [Fact]
    public void ResolvesTheBaseThatWasActuallyNamedRatherThanAnyClassOfThatName()
    {
        // Contracts.Order derives from Contracts.NamedBaseObject, which is persistent in no sense.
        // Both namespaces declare a NamedBaseObject, and only one of them is an entity.
        var orders = SampleProjects.DeepXpo.Entities.Where(entity => entity.ClassName == "Order").ToList();

        Assert.Equal(["SampleDeep.Module.BusinessObjects"], orders.Select(entity => entity.Namespace));
    }

    [Fact]
    public void ReadsTheRulesOfAnEntityFoundThroughItsBase()
    {
        // The class was invisible, so everything declared on it was too.
        var order = SampleProjects.DeepXpo.Entity("Order");

        Assert.Equal("Sales", order.NavigationGroup);
        Assert.Contains(order.Properties, property => property.Name == "Number");
    }
}
