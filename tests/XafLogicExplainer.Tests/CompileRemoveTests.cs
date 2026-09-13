using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Source in the project folder that the project file removes from the build (#86).
/// </summary>
/// <remarks>
/// The largest of six real applications keeps two such files, and the index listed both as classes of
/// the application: one in a navigation group, one under the name of a class that is compiled. The
/// index calls its entity list complete, so a class that is not compiled is a wrong answer nothing on
/// the page gives away.
/// </remarks>
public class CompileRemoveTests
{
    [Fact]
    public void TheApplicationHasTheOneInvoiceItCompiles()
    {
        var invoice = Assert.Single(Archive.Entities, entity => entity.ClassName == "Invoice");

        Assert.Equal("Archive.Module.BusinessObjects", invoice.Namespace);
    }

    [Fact]
    public void AClassInARemovedFolderIsNotListed() =>
        Assert.DoesNotContain("DraftStatement", Listed);

    [Fact]
    public void AClassInARemovedFolderIsNotInNavigation() =>
        Assert.DoesNotContain(Archive.Navigation, group => group.EntityClassNames.Contains("DraftStatement"));

    [Fact]
    public void AClassInARemovedFolderHasNoScreens() =>
        Assert.DoesNotContain(Archive.Views, view => view.Id.StartsWith("DraftStatement_", StringComparison.Ordinal));

    [Fact]
    public void AFileRemovedByAPatternIsNotListed() =>
        Assert.DoesNotContain("CustomerBackup", Listed);

    [Fact]
    public void AFileRemovedOnlyUnderAConditionIsStillListed() =>
        Assert.Contains("Supplier", Listed);

    [Fact]
    public void AControllerInARemovedFolderIsNotListed()
    {
        var controllers = Archive.Controllers.Select(controller => controller.ClassName).ToList();

        Assert.Contains("InvoiceController", controllers);
        Assert.DoesNotContain("LegacyInvoiceController", controllers);
    }

    [Fact]
    public void TheIndexNamesTheInvoiceWithoutANamespace()
    {
        var index = new AgentContextGenerator().GenerateIndex(Archive, []);

        Assert.Contains("| **Invoice** ", index);
        Assert.DoesNotContain("Catalog.Invoice", index);
    }

    [Fact]
    public void ARemovedFileStillCountsAsSomethingTheExtractionDependsOn()
    {
        // Putting the file back into the build changes the documentation, so change detection has to
        // keep watching it even though nothing is read from it today.
        var draft = Path.GetFullPath(Path.Combine(SampleProjects.CompileRemovePath, "BusinessObjects", "Drafts", "DraftStatement.cs"));

        Assert.Contains(draft, SourceRoster.Files(SampleProjects.CompileRemovePath), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnOldProjectFileThatListsItsSourcesByHandHidesNothing()
    {
        var listed = SampleProjects.MigratedProject.Entities.Select(entity => entity.ClassName).ToList();

        Assert.Contains("Account", listed);
        Assert.Contains("Holiday", listed);
    }

    [Fact]
    public void AModuleWithNoProjectFileIsNotGovernedByTheProjectFileAboveIt()
    {
        // The test project itself removes Fixtures\**\*.cs from its build, and this fixture has no
        // project file of its own. A rule that went looking upward would find the test project's.
        Assert.NotEmpty(SampleProjects.DeepXpo.Entities);
    }

    private static ExtractedProject Archive => SampleProjects.CompileRemove;

    private static List<string> Listed => [.. Archive.Entities.Select(entity => entity.ClassName)];
}
