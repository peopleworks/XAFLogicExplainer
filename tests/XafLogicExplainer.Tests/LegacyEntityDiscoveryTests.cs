namespace XafLogicExplainer.Tests;

/// <summary>
/// Entities that an application declares only by registering them in its DbContext.
/// </summary>
/// <remarks>
/// An XAF application on an existing schema rarely derives from <c>BaseObject</c>: the tables
/// bring their own keys, so the project writes its own base class, or maps a plain POCO. Matching
/// the class's direct base list against a list of XPO-era base type names finds none of them, and
/// the loss is silent — an entity that is never seen cannot be reported as missing.
/// <para>
/// For EF Core the application already states what its entities are, in the one place that has to
/// be right for it to run at all: the <c>DbSet&lt;T&gt;</c> properties of its DbContext.
/// </para>
/// </remarks>
public class LegacyEntityDiscoveryTests
{
    [Fact]
    public void FindsAnEntityOnAHandWrittenBaseClass()
    {
        var names = SampleProjects.LegacyEf.Entities.Select(entity => entity.ClassName);

        Assert.Contains("Invoice", names);
    }

    [Fact]
    public void FindsAMappedPocoWithNoXafBaseClassOrInterface()
    {
        var names = SampleProjects.LegacyEf.Entities.Select(entity => entity.ClassName);

        Assert.Contains("Warehouse", names);
    }

    [Fact]
    public void DoesNotReportFrameworkTypesRegisteredInTheDbContext()
    {
        var names = SampleProjects.LegacyEf.Entities.Select(entity => entity.ClassName).ToList();

        // ModulesInfo and FileData are DbSets too, but DevExpress declares those types, not this
        // application. A roster read from the DbContext must not turn the framework's own tables
        // into business entities the agent is told to reason about.
        Assert.DoesNotContain("ModuleInfo", names);
        Assert.DoesNotContain("FileData", names);
    }

    [Fact]
    public void ReadsThePropertiesOfADbSetRegisteredEntity()
    {
        var invoice = SampleProjects.LegacyEf.Entity("Invoice");

        Assert.Equal(["Id", "Number", "Amount", "IsPaid"], invoice.Properties.Select(p => p.Name));
    }

    [Fact]
    public void ReadsTheNavigationGroupOfADbSetRegisteredEntity()
    {
        Assert.Equal("Sales", SampleProjects.LegacyEf.Entity("Invoice").NavigationGroup);
    }
}
