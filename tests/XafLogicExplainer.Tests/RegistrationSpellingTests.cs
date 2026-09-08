namespace XafLogicExplainer.Tests;

/// <summary>
/// Which spellings of an EF Core registration are read, and which are not.
/// </summary>
/// <remarks>
/// The rule that a class declaring <c>DbSet&lt;T&gt;</c> properties is a context closed a silent
/// zero, and the follow-up question is what else people write. This is that survey, kept as tests
/// so the gaps stay measured instead of being rediscovered — the ones that pass and the ones that
/// do not are equally the point.
/// </remarks>
public class RegistrationSpellingTests
{
    // -------------------------------------------------------------------- read

    /// <summary>
    /// <c>public DbSet&lt;Epsilon&gt; Epsilons =&gt; Set&lt;Epsilon&gt;();</c>
    /// </summary>
    [Fact]
    public void AnExpressionBodiedDbSetIsARegistration()
    {
        Assert.Contains("Epsilon", Reported);
    }

    /// <summary>
    /// <c>public Microsoft.EntityFrameworkCore.DbSet&lt;Alpha&gt; Alphas { get; set; }</c>
    /// </summary>
    /// <remarks>
    /// What a file with no <c>using</c> for EF Core writes. The pattern demanded a bare generic
    /// name, so a whole context written this way registered nothing at all.
    /// </remarks>
    [Fact]
    public void AFullyQualifiedDbSetPropertyIsARegistration()
    {
        Assert.Contains("Alpha", Reported);
    }

    /// <summary>
    /// <c>public DbSet&lt;global::Shapes.BusinessObjects.Beta&gt; Betas { get; set; }</c>
    /// </summary>
    /// <remarks>
    /// What a generator emits when it wants to be safe about ambiguity. The alias qualifier
    /// survived into the namespace comparison, so the registration matched nothing.
    /// </remarks>
    [Fact]
    public void AGloballyQualifiedTypeArgumentIsARegistration()
    {
        Assert.Contains("Beta", Reported);
    }

    /// <summary>
    /// <c>class ShapesContext : IdentityDbContext&lt;AppUser&gt;</c>, with no <c>DbSet</c> for it.
    /// </summary>
    /// <remarks>
    /// The type argument is the whole registration of the user table in the template ASP.NET Core
    /// writes. Matched on the base's own name ending in <c>DbContext</c> rather than on a list, so
    /// it holds for a team's own generic context too, and the base itself is never needed.
    /// </remarks>
    [Fact]
    public void TheTypeArgumentOfAGenericContextBaseIsARegistration()
    {
        Assert.Contains("AppUser", Reported);
    }

    // -------------------------------------------------------------------- not read, on purpose

    /// <summary>
    /// <c>modelBuilder.Entity&lt;Gamma&gt;().ToTable("gamma")</c> and nothing else.
    /// </summary>
    /// <remarks>
    /// A real registration that is not read. Following it means reading a method body for calls
    /// rather than a declaration for a shape, and a fluent chain built in a loop or behind a
    /// helper would defeat that anyway. Asserted so the limit is a decision on the record.
    /// </remarks>
    [Fact]
    public void AFluentMappingCallIsNotRead()
    {
        Assert.DoesNotContain("Gamma", Reported);
    }

    /// <summary>
    /// A context in <c>Data/</c> while a <c>BusinessObjects/</c> folder exists.
    /// </summary>
    /// <remarks>
    /// Discovery narrows to the business-object folder when there is one, so the second context is
    /// never parsed and everything only it registers is missed. Widening the scan changes what
    /// every project pays to extract, which makes it its own decision rather than a detail of this
    /// one.
    /// </remarks>
    [Fact]
    public void AContextOutsideTheBusinessObjectFolderIsNotRead()
    {
        Assert.DoesNotContain("Zeta", Reported);
    }

    private static List<string> Reported =>
        [.. SampleProjects.RegistrationSpellings.Entities.Select(entity => entity.ClassName)];
}
