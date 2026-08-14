namespace XafLogicExplainer.Tests;

/// <summary>
/// Properties an entity inherits from a class declared in the same project.
/// </summary>
/// <remarks>
/// Finding a class through its base (<see cref="DeepInheritanceTests"/>) made the class visible
/// but left two thirds of its columns out: <c>PriorityOrder</c> was reported with <c>Rank</c>
/// alone, while the <c>Name</c> and <c>Number</c> it persists lived under other headings. An
/// inventory that promises to be complete has to carry them on the entity itself.
/// </remarks>
public class InheritedPropertyFoldTests
{
    [Fact]
    public void FoldsInheritedPropertiesIntoTheEntityThatInheritsThem()
    {
        var priorityOrder = SampleProjects.DeepXpo.Entity("PriorityOrder");

        // Declaration order from the root down, the way the columns read in the class.
        Assert.Equal(["Name", "Number", "Rank"], priorityOrder.Properties.Select(property => property.Name));
    }

    [Fact]
    public void NamesTheClassThatDeclaredAnInheritedProperty()
    {
        var priorityOrder = SampleProjects.DeepXpo.Entity("PriorityOrder");

        // The declarer, not the parent: Name comes from NamedBaseObject even though PriorityOrder
        // reaches it through Order.
        Assert.Equal(
            ["NamedBaseObject", "Order", null],
            priorityOrder.Properties.Select(property => property.InheritedFrom));
    }

    [Fact]
    public void APropertyTheClassRedeclaresIsItsOwn()
    {
        var labeledOrder = SampleProjects.DeepXpo.Entity("LabeledOrder");

        // One Number, not two -- and it is the redeclaration, not the inherited one.
        Assert.Equal(["Name", "Number"], labeledOrder.Properties.Select(property => property.Name));
        Assert.Null(labeledOrder.Properties.Single(property => property.Name == "Number").InheritedFrom);
    }

    [Fact]
    public void TheBaseItselfKeepsOnlyWhatItDeclares()
    {
        var namedBase = SampleProjects.DeepXpo.Entity("NamedBaseObject");

        Assert.Equal(["Name"], namedBase.Properties.Select(property => property.Name));
    }

    [Fact]
    public void MarksTheAbstractBaseAsAbstract()
    {
        // With every descendant carrying the base's columns, a renderer needs to know which
        // heading is a table and which is a convention.
        Assert.True(SampleProjects.DeepXpo.Entity("NamedBaseObject").IsAbstract);
        Assert.False(SampleProjects.DeepXpo.Entity("Order").IsAbstract);
    }
}
