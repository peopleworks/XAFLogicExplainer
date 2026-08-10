namespace XafLogicExplainer.Tests;

/// <summary>
/// Reading module setup: which XAF modules are required, and which business classes are exported.
/// </summary>
/// <remarks>
/// Regression cover for a bug that made generated documentation authoritatively wrong. Required
/// modules were collected from any invocation whose expression contained "Add", which matched
/// every <c>.Add()</c> call in the module constructor — so
/// <c>AdditionalExportedTypes.Add(typeof(Customer))</c> reported a business entity as a required
/// XAF module. On a real application that put nine entities and six framework base types into a
/// list of twelve genuine modules.
/// </remarks>
public class ModuleAnalyzerTests
{
    [Fact]
    public void FindsTheModuleClass()
    {
        var module = SampleProjects.Xpo.ModuleInfo;

        Assert.NotNull(module);
        Assert.Equal("SampleAppModule", module!.ModuleClassName);
    }

    [Fact]
    public void ReportsRequiredModules()
    {
        var required = SampleProjects.Xpo.ModuleInfo!.RequiredModules;

        Assert.Contains(required, m => m.Contains("SystemModule", StringComparison.Ordinal));
        Assert.Contains(required, m => m.Contains("ValidationModule", StringComparison.Ordinal));
        Assert.Contains(required, m => m.Contains("ConditionalAppearanceModule", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Order")]
    [InlineData("OrderLine")]
    public void DoesNotReportBusinessEntitiesAsRequiredModules(string entityName)
    {
        var required = SampleProjects.Xpo.ModuleInfo!.RequiredModules;

        Assert.DoesNotContain(entityName, required);
    }

    [Fact]
    public void ReportsOnlyGenuineModules()
    {
        var required = SampleProjects.Xpo.ModuleInfo!.RequiredModules;

        // Every entry must actually name a module. The fixture adds three of them, alongside three
        // business classes on a different collection in the same constructor.
        Assert.Equal(3, required.Count);
        Assert.All(required, m => Assert.Contains("Module", m, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Order")]
    [InlineData("OrderLine")]
    public void RecordsExportedTypesWhereTheyBelong(string entityName)
    {
        var registered = SampleProjects.Xpo.ModuleInfo!.RegisteredTypes;

        Assert.Contains(entityName, registered);
    }
}
