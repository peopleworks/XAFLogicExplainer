using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The lists that promise to be complete.
/// </summary>
/// <remarks>
/// A worse failure than a wrong entry: an inventory headed "every expression in this application"
/// that holds four of six kinds reads as authoritative, and a reader has no way to know what is
/// missing. The tool's whole argument is that a closed-world statement is only useful if it is
/// true, so its own inventories have to be checked rather than remembered.
/// </remarks>
public class CompletenessTests
{
    [Theory]
    [InlineData("ExpiresOn > IssuedOn")]      // RuleCriteria, positional third argument
    [InlineData("Total >= 0")]                // RuleCriteria on another entity
    [InlineData("Not IsDispensed")]           // the action's TargetObjectsCriteria
    [InlineData("IsOnDuty = True")]           // a lookup's DataSourceCriteria
    [InlineData("OnHand = 0")]                // an appearance rule
    public void TheCriteriaIndexHoldsEveryKindOfExpression(string expression)
    {
        var criteria = CodebaseConventions.Infer(SampleProjects.Demo).CriteriaExamples;

        Assert.Contains(criteria, c => c.Expression == expression);
    }

    [Fact]
    public void ReadsTheExpressionARuleEnforces()
    {
        // RuleCriteria's overloads put the criteria in different positions, and the expression is
        // not the same field as the criteria that decides when the rule applies. Only the second
        // was read, so the rule itself was absent.
        var rule = SampleProjects.Demo.Entity("Prescription").ValidationRules
            .Single(r => r.RuleType.Contains("Criteria", StringComparison.Ordinal));

        Assert.Equal("ExpiresOn > IssuedOn", rule.Expression);
    }

    [Fact]
    public void ReadsWhenAnActionCanBePressed()
    {
        // The condition governing the demo's only operation, and it was in no generated document.
        var action = SampleProjects.Demo.Controller("DispenseController").Actions
            .Single(a => a.ActionId == "DispensePrescription");

        Assert.Equal("Not IsDispensed", action.TargetObjectsCriteria);
    }

    [Fact]
    public void ReadsAUniqueIndex()
    {
        // A constraint the user meets as a save that fails, enforced below the application, and
        // captured nowhere. [Indexed] alone is a performance hint; the Unique argument is a rule.
        Assert.True(SampleProjects.Demo.Entity("Product").Property("Barcode").IsUnique);
        Assert.False(SampleProjects.Demo.Entity("Product").Property("Name").IsUnique);
    }

    [Fact]
    public void PutsDefaultClassOptionsClassesInTheMenu()
    {
        // [DefaultClassOptions] with no [NavigationItem] means XAF's Default group -- still in the
        // menu. They were dropped from an inventory headed "what a user sees in the menu".
        var groups = SampleProjects.Demo.Navigation;
        var named = SampleProjects.Demo.Entities
            .Where(e => e.IsDefaultClassOptions && string.IsNullOrEmpty(e.NavigationGroup))
            .Select(e => e.ClassName)
            .ToList();

        Assert.NotEmpty(named);

        var defaultGroup = groups.Single(g => g.GroupName == "Default");

        Assert.Equal(named.Order(), defaultGroup.EntityClassNames.Order());
    }

    [Fact]
    public void DoesNotFileACollectionAsACalculatedProperty()
    {
        // An XPO collection property is getter-only, so it satisfied IsComputed and appeared under
        // derived logic -- inviting a reader to treat a persistent relationship as a formula.
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.Demo);
        var rules = sections.Single(s => s.FileName.EndsWith("_BusinessRules", StringComparison.Ordinal));

        var derived = rules.Content[rules.Content.IndexOf("Computed", StringComparison.OrdinalIgnoreCase)..];

        Assert.DoesNotContain("XPCollection", derived);
    }

    [Fact]
    public void SaysWhatAnAppearanceRuleDoesAndWhere()
    {
        // "when OnHand = 0 ()" -- a condition with no consequence, on no stated screen.
        var sections = new MarkdownDocumentationGenerator("en").GenerateSections(SampleProjects.Demo);
        var rules = sections.Single(s => s.FileName.EndsWith("_BusinessRules", StringComparison.Ordinal));

        Assert.Contains("font colour=Red", rules.Content);
        Assert.Contains("in ListView", rules.Content);
    }

    [Fact]
    public void CountsInterfacesAsAncestorsToo()
    {
        // XAF's object-type test is IsAssignableFrom, which an interface satisfies -- DevExpress
        // targets IAuthenticationStandardUser, not a class. Following the base class alone made
        // every interface-targeted controller match no view at all, silently.
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Sale" },
            new() { Id = "Patient_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Patient" },
        };

        var controller = new ExtractedController
        {
            ClassName = "AuditController",
            Targeting = new ControllerTargeting { TargetObjectType = "IAudited" },
        };

        var entities = new List<ExtractedEntity>
        {
            new() { ClassName = "Sale", BaseType = "BaseObject", BaseTypes = ["BaseObject", "IAudited"] },
            new() { ClassName = "Patient", BaseType = "BaseObject", BaseTypes = ["BaseObject"] },
        };

        ViewActivationResolver.Resolve(views, [controller], entities);

        Assert.Single(views[0].Activates);
        Assert.Empty(views[1].Activates);
    }

    [Fact]
    public void InheritsAnInterfaceThroughABaseClass()
    {
        var views = new List<ExtractedView>
        {
            new() { Id = "Sale_DetailView", ViewType = ModelViewType.DetailView, ObjectType = "Sale" },
        };

        var controller = new ExtractedController
        {
            ClassName = "AuditController",
            Targeting = new ControllerTargeting { TargetObjectType = "IAudited" },
        };

        var entities = new List<ExtractedEntity>
        {
            new() { ClassName = "Sale", BaseTypes = ["AuditedBase"] },
            new() { ClassName = "AuditedBase", BaseTypes = ["BaseObject", "IAudited"] },
        };

        ViewActivationResolver.Resolve(views, [controller], entities);

        Assert.Single(views[0].Activates);
    }

    [Fact]
    public void DoesNotCallTheCriteriaIndexExhaustiveWhileDeduplicating()
    {
        // The collector groups by expression on purpose. The page said "every expression in this
        // application", which is a different promise from the one the code keeps.
        var html = new HtmlExplainerGenerator("0.12.0").Generate(SampleProjects.Demo);

        Assert.DoesNotContain("Every expression in this application", html);
        Assert.Contains("distinct", html);
    }
}
