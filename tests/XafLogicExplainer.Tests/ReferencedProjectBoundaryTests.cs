using XafLogicExplainer.Core.Hashing;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That reading a referenced project stays a way to resolve what this one declares, and never
/// becomes a way for the other project to change what this one is.
/// </summary>
/// <remarks>
/// Reading referenced source fixed a silent zero, and paid for it with a boundary that had to be
/// respected in three separate places. All three were reported after that shipped: a class both
/// projects declare, an ORM decided by the wrong project, and a change hash that no longer covered
/// everything the extraction reads.
/// </remarks>
public class ReferencedProjectBoundaryTests
{
    // ------------------------------------------------- a name two projects both declare

    /// <summary>
    /// The module's own class survives, with its own shape.
    /// </summary>
    /// <remarks>
    /// Parts were merged on namespace and name alone, so a class the module and its library both
    /// declared under one namespace was treated as two halves of one partial class. When the
    /// borrowed half became primary the merged entity carried the borrowed file path, and the
    /// removal that drops borrowed classes deleted the module's own entity with it: the fixture
    /// reported <em>no entities at all</em>.
    /// </remarks>
    [Fact]
    public void AClassDeclaredInBothProjectsKeepsTheModulesOwnShape()
    {
        var cliente = SampleProjects.Homonym.Entity("Cliente");

        Assert.Equal(["Id", "Nombre", "Correo"], cliente.Properties.Select(p => p.Name));
    }

    /// <summary>
    /// And nothing from the library's copy leaks into it.
    /// </summary>
    /// <remarks>
    /// The other direction of the same defect: when the local half declared a base list it stayed
    /// primary and simply absorbed the borrowed properties, so the module was reported as
    /// modelling a column that exists in a different project.
    /// </remarks>
    [Fact]
    public void TheLibrarysCopyDoesNotLeakItsPropertiesIn()
    {
        var cliente = SampleProjects.Homonym.Entity("Cliente");

        Assert.DoesNotContain("CodigoHeredado", cliente.Properties.Select(p => p.Name));
    }

    /// <summary>
    /// C# binds the local type, and so does this.
    /// </summary>
    /// <remarks>
    /// One name is one type. The compiler resolves the collision in favour of the local
    /// declaration (CS0436) and warns; the extraction has to agree with the compiler, because an
    /// agent reading the document will be writing code against whichever one really binds.
    /// </remarks>
    [Fact]
    public void OnlyOneClienteIsReported()
    {
        Assert.Single(SampleProjects.Homonym.Entities, e => e.ClassName == "Cliente");
    }

    // ------------------------------------------------- whose ORM is it

    /// <summary>
    /// An XPO application that references an EF Core utility is an XPO application.
    /// </summary>
    /// <remarks>
    /// The roster was built over both pools and `DetectOrmType` answered EF Core the moment
    /// anything registered, so a cache store next door decided how the application persists. The
    /// ORM is what <c>AGENTS.md</c> and the MCP overview hand an agent as a hard rule, and that
    /// rule forbids the entire API surface of whichever ORM it did not name — so being wrong here
    /// costs more than being silent would.
    /// </remarks>
    [Fact]
    public void AnXpoModuleReferencingAnEfCoreUtilityIsStillXpo()
    {
        Assert.Equal("XPO", SampleProjects.XpoWithEfUtil.OrmType);
    }

    /// <summary>
    /// Its own entity is kept and the utility's row type is not.
    /// </summary>
    [Fact]
    public void TheUtilitysOwnTypesStayOutOfTheInventory()
    {
        var names = SampleProjects.XpoWithEfUtil.Entities.Select(e => e.ClassName).ToList();

        Assert.Contains("Expediente", names);
        Assert.DoesNotContain("CacheRow", names);
    }

    /// <summary>
    /// A module that names no ORM itself still inherits the reading from what it references.
    /// </summary>
    /// <remarks>
    /// The fallback, and the reason the rule is "own first" rather than "own only": the opposite
    /// layout is just as real, and answering <c>Unknown</c> there would throw away a reading that
    /// was there to be had.
    /// </remarks>
    [Fact]
    public void AModuleThatNamesNoOrmStillResolvesItThroughItsReference()
    {
        Assert.Equal("XPO", SampleProjects.SharedBase.OrmType);
    }

    // ------------------------------------------------- the hash has to cover what is read

    /// <summary>
    /// Editing a base class in a referenced project invalidates the documentation.
    /// </summary>
    /// <remarks>
    /// The hash read only the project's own tree, so a shared base could change the shape of every
    /// entity below it and the tool would answer "no changes detected". That is worse than having
    /// no change detection at all: nobody re-runs a command that just said there was nothing to do,
    /// so the document stays wrong and stays confident.
    /// </remarks>
    [Fact]
    public void EditingAReferencedFileChangesTheHash()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xle-hash-{Guid.NewGuid():N}");
        var module = Path.Combine(root, "solution", "App.Module");
        var library = Path.Combine(root, "library", "Shared");

        Directory.CreateDirectory(Path.Combine(module, "BusinessObjects"));
        Directory.CreateDirectory(library);

        try
        {
            File.WriteAllText(Path.Combine(library, "Shared.csproj"), "<Project />");
            var shared = Path.Combine(library, "AuditedEntity.cs");
            File.WriteAllText(shared, "namespace Shared; public abstract class AuditedEntity { public DateTime CreatedOn { get; set; } }");

            var project = Path.Combine(module, "App.Module.csproj");
            File.WriteAllText(project,
                """<Project><ItemGroup><ProjectReference Include="..\..\library\Shared\Shared.csproj" /></ItemGroup></Project>""");
            File.WriteAllText(Path.Combine(module, "BusinessObjects", "Factura.cs"),
                "namespace App; public class Factura : Shared.AuditedEntity { public decimal Total { get; set; } }");

            var calculator = new ProjectHashCalculator();
            var before = calculator.ComputeHash(module);

            File.AppendAllText(shared, "\n// a property arrives on the shared base");
            Assert.NotEqual(before, calculator.ComputeHash(module));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    /// So does adding or removing the reference itself.
    /// </summary>
    /// <remarks>
    /// A <c>ProjectReference</c> decides what is read, and editing one changes the extraction
    /// without changing a line of C#.
    /// </remarks>
    [Fact]
    public void EditingTheProjectFileChangesTheHash()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xle-hash-{Guid.NewGuid():N}");
        var module = Path.Combine(root, "App.Module");

        Directory.CreateDirectory(module);

        try
        {
            var project = Path.Combine(module, "App.Module.csproj");
            File.WriteAllText(project, "<Project />");
            File.WriteAllText(Path.Combine(module, "Factura.cs"), "namespace App; public class Factura { }");

            var calculator = new ProjectHashCalculator();
            var before = calculator.ComputeHash(module);

            File.WriteAllText(project,
                """<Project><ItemGroup><ProjectReference Include="..\Nowhere\Nowhere.csproj" /></ItemGroup></Project>""");

            Assert.NotEqual(before, calculator.ComputeHash(module));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
