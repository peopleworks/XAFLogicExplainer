using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That a module whose entity base lives in a referenced project is read, not emptied.
/// </summary>
/// <remarks>
/// Matching a class base list against known names finds nothing when the base is declared in
/// another project, and the loss is total rather than partial: the module reports that it persists
/// nothing, which is a claim and a wrong one. A developer with a shared framework library is
/// exactly who this tool is for and exactly who was getting nothing back.
/// <para>
/// The fixture puts the base two references away, in a library outside the module solution folder,
/// because both shapes that came before it — a base in the same project, and each application
/// carrying its own copy — already passed while this one extracted zero.
/// </para>
/// </remarks>
public class SharedBaseTests
{
    // --------------------------------------------------------- the reference chain itself

    [Fact]
    public void FollowsAReferenceToTheProjectItNames()
    {
        var referenced = ProjectFile.ReferencedDirectories(SampleProjects.SharedBasePath);

        Assert.Contains(referenced, path => Path.GetFileName(path) == "Audit.Core");
    }

    /// <summary>
    /// And keeps going, because a base is as likely to be two hops away as one.
    /// </summary>
    /// <remarks>
    /// An application references its own framework project, which references the audit
    /// primitives. Stopping at the first hop would fix the shallow case and leave the ordinary
    /// one failing in exactly the same silence.
    /// </remarks>
    [Fact]
    public void FollowsReferencesTransitively()
    {
        var referenced = ProjectFile.ReferencedDirectories(SampleProjects.SharedBasePath);

        Assert.Contains(referenced, path => Path.GetFileName(path) == "Audit.Primitives");
    }

    /// <summary>
    /// A cycle terminates rather than hanging the extraction.
    /// </summary>
    /// <remarks>
    /// MSBuild forbids one, so this is about a hand-edited or half-migrated project file rather
    /// than about anything that builds. Hanging on it would be the worst possible failure, because
    /// it happens before any output exists to hint at why.
    /// </remarks>
    [Fact]
    public void AReferenceCycleTerminates()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xle-cycle-{Guid.NewGuid():N}");
        var first = Path.Combine(root, "First");
        var second = Path.Combine(root, "Second");

        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        try
        {
            File.WriteAllText(Path.Combine(first, "First.csproj"),
                """<Project><ItemGroup><ProjectReference Include="..\Second\Second.csproj" /></ItemGroup></Project>""");
            File.WriteAllText(Path.Combine(second, "Second.csproj"),
                """<Project><ItemGroup><ProjectReference Include="..\First\First.csproj" /></ItemGroup></Project>""");

            var referenced = ProjectFile.ReferencedDirectories(first);

            Assert.Single(referenced);
            Assert.Equal("Second", Path.GetFileName(referenced[0]));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ------------------------------------------------------------------ what gets extracted

    [Fact]
    public void EntitiesOnAReferencedBaseAreFound()
    {
        var names = SampleProjects.SharedBase.Entities.Select(entity => entity.ClassName);

        Assert.Contains("Cliente", names);
        Assert.Contains("Factura", names);
    }

    /// <summary>
    /// Both hops of the chain fold their properties down.
    /// </summary>
    /// <remarks>
    /// Finding the entity is half the answer. An entity carries what it inherits so a reader of
    /// one entity is told the whole truth, and a <c>Cliente</c> reported without the
    /// <c>CreatedOn</c> every row in the table has is a different kind of wrong from not being
    /// reported at all.
    /// </remarks>
    [Fact]
    public void PropertiesInheritedThroughBothHopsAreFolded()
    {
        var cliente = SampleProjects.SharedBase.Entity("Cliente");
        var names = cliente.Properties.Select(property => property.Name).ToList();

        Assert.Contains("CreatedBy", names);   // Audit.Core.AuditedEntity, one hop away
        Assert.Contains("CreatedOn", names);   // Audit.Primitives.TrackedObject, two hops away
        Assert.Contains("Nombre", names);
    }

    /// <summary>
    /// The library classes are read, and are not this application business objects.
    /// </summary>
    /// <remarks>
    /// They belong to the project that declares them. Listing them here would put the same shared
    /// base in the inventory of every client that references it, and the cross-project wiki would
    /// then report one library referenced three times as a class modelled three times — the false
    /// reuse claim that <see href="https://github.com/peopleworks/XAFLogicExplainer/issues/54"/>
    /// was about, arriving by a different road.
    /// </remarks>
    [Fact]
    public void TheReferencedLibraryClassesAreNotListedAsThisApplicationEntities()
    {
        var names = SampleProjects.SharedBase.Entities.Select(entity => entity.ClassName).ToList();

        Assert.DoesNotContain("AuditedEntity", names);
        Assert.DoesNotContain("TrackedObject", names);
        Assert.Equal(2, names.Count);
    }

    /// <summary>
    /// The ORM is read from the whole picture, including the referenced base.
    /// </summary>
    /// <remarks>
    /// The module own files name no XPO base class at all — the nearest one is in the library —
    /// so an application reading only its own directory could reasonably have said Unknown.
    /// </remarks>
    [Fact]
    public void TheOrmIsResolvedThroughTheReferencedBase()
    {
        Assert.Equal("XPO", SampleProjects.SharedBase.OrmType);
    }

    /// <summary>
    /// With reference following switched off, the old answer comes back.
    /// </summary>
    /// <remarks>
    /// Pinned so the switch is known to do something. It exists because the cost is not free: a
    /// referenced project with no <c>BusinessObjects</c> folder is parsed in full, which is
    /// exactly what <c>Audit.Primitives</c> is here to represent.
    /// </remarks>
    [Fact]
    public void TheSwitchTurnsItOff()
    {
        var options = new ExtractionOptions
        {
            IncludeSourceCode = true,
            FollowProjectReferences = false,
        };

        var entities = new EntityAnalyzer().AnalyzeEntities(SampleProjects.SharedBasePath, options);

        Assert.Empty(entities);
    }
}
