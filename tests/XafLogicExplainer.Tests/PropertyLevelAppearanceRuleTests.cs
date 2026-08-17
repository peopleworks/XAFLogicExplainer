using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// An <c>[Appearance]</c> rule written on a property, which is the first form the documentation
/// teaches.
/// </summary>
/// <remarks>
/// <c>AppearanceAttribute</c> is declared
/// <c>[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property
/// | AttributeTargets.Interface)]</c>, and "Declare Conditional Appearance Rules in Code" lists
/// applying it to a property as Approach 1 and applying it to the class with the property named in
/// <c>TargetItems</c> as Approach 2 — two spellings of one rule.
/// <para>
/// Only the class-level spelling was read. Every <c>[Appearance]</c> in every fixture happened to
/// be class-level, so the whole suite agreed that reading
/// <see cref="Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax.AttributeLists"/> alone
/// was enough — the same shape as the <c>CustomMessageTemplate</c> blind spot in
/// <see cref="ValidationRuleArgumentTests"/>, where a form no fixture used could not be seen to be
/// missing. <c>ExtractValidationRules</c> had walked the properties as well as the class since the
/// beginning; <c>ExtractAppearanceRules</c> never did.
/// </para>
/// <para>
/// A rule that governs a property and is reported nowhere is worse than an unread one: the entity's
/// section is presented as its complete inventory, so the reader concludes the property is
/// unconditionally editable.
/// </para>
/// </remarks>
public class PropertyLevelAppearanceRuleTests
{
    private static ExtractedEntity Product => SampleProjects.Demo.Entity("Product");

    private static ExtractedAppearanceRule? PriceRule => Product.AppearanceRules
        .SingleOrDefault(rule => rule.Id == "PriceLockedOnPrescriptionItems");

    [Fact]
    public void ARuleWrittenOnAPropertyIsFound()
    {
        Assert.NotNull(PriceRule);
    }

    [Fact]
    public void ItKeepsTheCriteriaThatDecidesWhenItApplies()
    {
        Assert.Equal("RequiresPrescription", PriceRule!.Criteria);
    }

    [Fact]
    public void ItTargetsThePropertyItWasWrittenOn()
    {
        // Approach 2 spells this rule on the class with TargetItems = "UnitPrice". The two
        // approaches are equivalent, so they must extract alike -- otherwise which spelling the
        // author happened to choose changes what the documentation says the rule affects.
        Assert.Equal("UnitPrice", PriceRule!.TargetItems);
    }

    [Fact]
    public void ItDoesNotDisplaceTheRuleWrittenOnTheClass()
    {
        Assert.Contains(Product.AppearanceRules, rule => rule.Id == "ProductOutOfStock");
    }

    [Fact]
    public void AnExplicitTargetItemsOnAPropertyRuleIsLeftAlone()
    {
        // Only an unset TargetItems is filled in from the property name. StockBatch's rule names
        // its own targets, and inferring over the top of that would silently narrow the rule.
        var batchRule = SampleProjects.Demo.Entity("StockBatch").AppearanceRules
            .Single(rule => rule.Id == "BatchExpired");

        Assert.Equal("*", batchRule.TargetItems);
    }
}
