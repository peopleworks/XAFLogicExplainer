namespace XafLogicExplainer.Tests;

/// <summary>
/// Telling XPO and EF Core apart.
/// </summary>
/// <remarks>
/// Both are legitimate XAF and their base classes share names — <c>BaseObject</c> exists in each,
/// in different namespaces — so the ORM has to be decided from the <c>using</c> directives. Getting
/// it wrong makes every downstream suggestion wrong in a way that still looks like valid XAF.
/// </remarks>
public class OrmDetectionTests
{
    [Fact]
    public void DetectsXpo() =>
        Assert.Equal("XPO", SampleProjects.Xpo.OrmType, ignoreCase: true);

    [Fact]
    public void DetectsEfCoreFromItsNamespace() =>
        Assert.Contains("EF", SampleProjects.EfCore.OrmType, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void FindsEfCoreEntitiesDespiteTheSharedBaseClassName()
    {
        var names = SampleProjects.EfCore.Entities.Select(e => e.ClassName).OrderBy(n => n).ToList();

        Assert.Equal(["Category", "Product"], names);
    }

    [Fact]
    public void ReadsEfCoreStringLengthAsASize()
    {
        var title = SampleProjects.EfCore.Entity("Product").Property("Title");

        Assert.Equal(150, title.Size);
        Assert.True(title.IsRequired);
    }

    [Fact]
    public void ReadsMaxLengthAsWellAsStringLength()
    {
        Assert.Equal(400, SampleProjects.EfCore.Entity("Category").Property("Notes").Size);
    }

    [Fact]
    public void RecognizesEfCoreCollectionsAsRelationships()
    {
        // IList<Product> carries no [Association]; the relationship is inferred from the type.
        var products = Assert.Single(
            SampleProjects.EfCore.Entity("Category").Relationships,
            r => r.RelatedEntity == "Product");

        Assert.Equal("Products", products.PropertyName);
    }

    [Fact]
    public void RecognizesEfCoreReferenceProperties()
    {
        Assert.Contains(
            SampleProjects.EfCore.Entity("Product").Relationships,
            r => r.RelatedEntity == "Category");
    }

    [Fact]
    public void ResolvesNameOfInEfCoreFixturesToo() =>
        Assert.Equal("Title", SampleProjects.EfCore.Entity("Product").DefaultProperty);
}
