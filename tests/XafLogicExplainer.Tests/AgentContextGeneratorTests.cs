using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The always-loaded context written to <c>AGENTS.md</c>.
/// </summary>
/// <remarks>
/// These assertions are about the claims the file makes, not its wording. The ground rules are the
/// part that changes an agent's behavior, so a refactor that quietly drops one should fail here.
/// </remarks>
public class AgentContextGeneratorTests
{
    private static string XpoIndex =>
        new AgentContextGenerator("0.9.0").GenerateIndex(SampleProjects.Xpo, ["entities.md"]);

    private static string EfCoreIndex =>
        new AgentContextGenerator("0.9.0").GenerateIndex(SampleProjects.EfCore, []);

    private static string NoOrmIndex =>
        new AgentContextGenerator("0.9.0").GenerateIndex(SampleProjects.NoOrm, []);

    [Fact]
    public void DoesNotRuleOutAnOrmItNeverFoundEvidenceFor()
    {
        // The ground rule is emphatic in both directions on purpose -- which is exactly why it must
        // not fire on a guess. Telling an agent that DbContext "does not exist in this application"
        // is a harder failure than saying nothing, because it forbids the correct answer.
        var index = NoOrmIndex;

        Assert.DoesNotContain("Persistence is DevExpress XPO", index);
        Assert.DoesNotContain("Persistence is Entity Framework Core", index);
    }

    [Fact]
    public void StatesThatTheInventoriesAreComplete()
    {
        // The load-bearing claim: it converts "I did not find it" into "it does not exist".
        Assert.Contains("complete", XpoIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not exist", XpoIndex, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ForbidsEntityFrameworkInAnXpoApplication()
    {
        var index = XpoIndex;

        Assert.Contains("XPO", index);
        Assert.Contains("DbContext", index);
        Assert.Contains("never be suggested", index);
    }

    [Fact]
    public void ForbidsXpoInAnEntityFrameworkApplication()
    {
        var index = EfCoreIndex;

        Assert.Contains("Entity Framework Core", index);
        Assert.Contains("XPCollection", index);
        Assert.Contains("never be suggested", index);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Order")]
    [InlineData("OrderLine")]
    public void ListsEveryEntity(string entityName) =>
        Assert.Contains(entityName, XpoIndex, StringComparison.Ordinal);

    [Fact]
    public void ListsActionsWithTheirCaptions()
    {
        var index = XpoIndex;

        Assert.Contains("Approve", index);
        Assert.Contains("ApproveOrderController", index);
    }

    [Fact]
    public void ReportsConventionsInferredFromTheCodebase()
    {
        var index = XpoIndex;

        Assert.Contains("SampleApp.Module.BusinessObjects", index);
        Assert.Contains("XPCustomObject", index);
        Assert.Contains("BusinessObjects", index);
    }

    [Fact]
    public void QuotesRealCriteriaExpressions()
    {
        // XAF's criteria dialect is neither SQL nor C#; worked examples from the codebase teach it
        // better than a description does.
        Assert.Contains("IsBlocked = False", XpoIndex);
    }

    [Fact]
    public void OmitsTrivialCriteriaThatTeachNothing()
    {
        var conventions = CodebaseConventions.Infer(SampleProjects.Xpo);

        Assert.DoesNotContain(conventions.CriteriaExamples, c => c.Expression.Trim() == "1=1");
        Assert.All(conventions.CriteriaExamples, c => Assert.Contains(c.Expression, ch => char.IsLetter(ch)));
    }

    [Fact]
    public void WarnsThatModelEditorBehaviorIsInvisibleInCode() =>
        Assert.Contains("Model Editor", XpoIndex);

    [Fact]
    public void SaysHowToRegenerateItself() =>
        Assert.Contains("xaflogic agents", XpoIndex);

    [Fact]
    public void PointsAtTheDetailFilesItWasGivenWithoutInliningThem()
    {
        var index = XpoIndex;

        Assert.Contains("entities.md", index);
        // Sanity check on the whole point of tiering: the index stays an index.
        Assert.True(index.Length < 30_000, $"The index grew to {index.Length} characters.");
    }

    [Fact]
    public void GroundRulesStandAloneForClientsThatCannotImport()
    {
        // Copilot's instructions file has no import mechanism, so this block ships inline there
        // and must point at AGENTS.md rather than at "this file".
        var groundRules = new AgentContextGenerator("0.9.0").GenerateGroundRules(SampleProjects.Xpo);

        Assert.Contains("AGENTS.md", groundRules);
        Assert.Contains("XPO", groundRules);
        Assert.DoesNotContain("## Ground rules", groundRules);
    }

    [Fact]
    public void InfersConventionsWithoutInventingThem()
    {
        var conventions = CodebaseConventions.Infer(SampleProjects.Xpo);

        Assert.Equal("XPCustomObject", conventions.DominantEntityBaseType);
        Assert.True(conventions.UsesNamedAssociations);
        Assert.True(conventions.UsesPersistentAlias);
        Assert.True(conventions.UsesValidationAttributes);
        Assert.True(conventions.UsesAppearanceRules);
        Assert.True(conventions.HasModelEditorCustomizations);
    }
}
