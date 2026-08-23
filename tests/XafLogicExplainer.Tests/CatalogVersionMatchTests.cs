using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That a framework claim says which DevExpress release it came from, and admits when that is not
/// the release the application uses.
/// </summary>
/// <remarks>
/// The catalog used to be chosen by file date: <c>LoadLatest</c> took the newest one on the machine
/// whatever the application targeted, and the output said "these controllers load onto this screen"
/// in the same confident sentence either way. On a machine holding a single 26.1 catalog that
/// sentence was produced for a 23.2 application and for a 17.1 one — three releases and nine years
/// out respectively — with nothing to tell the reader.
/// <para>
/// Caught by the external review at 0.12.0 and still live at 0.15.0. The fixtures below are the
/// two project-file spellings taken from real applications, because the legacy one is what made the
/// first attempt at this wrong: a pre-NuGet XAF project has no <c>PackageReference</c> at all.
/// </para>
/// </remarks>
public class CatalogVersionMatchTests
{
    // --------------------------------------------------- reading the declaration

    /// <summary>An SDK-style project, as XAF has shipped since the move to NuGet.</summary>
    private const string ModernProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net7.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="DevExpress.ExpressApp" Version="23.2.5" />
            <PackageReference Include="DevExpress.ExpressApp.Blazor.All" Version="23.2.5" />
            <PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>
    /// A project from before XAF used NuGet, which names the version only inside the assembly name.
    /// </summary>
    private const string LegacyProject = """
        <?xml version="1.0" encoding="utf-8"?>
        <Project DefaultTargets="Build" ToolsVersion="4.0">
          <PropertyGroup>
            <TargetFrameworkVersion>v4.5.2</TargetFrameworkVersion>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="DevExpress.ExpressApp.v17.1, Version=17.1.4.0, Culture=neutral, PublicKeyToken=b88d1754d700e49a, processorArchitecture=MSIL" />
            <Reference Include="DevExpress.ExpressApp.Xpo.v17.1" />
            <Reference Include="System.Data" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void ReadsTheVersionAnSdkStyleProjectDeclares()
    {
        Assert.Equal("23.2", DeclaredDevExpressVersion.FromProjectFile(ModernProject));
    }

    [Fact]
    public void ReadsTheVersionOutOfALegacyAssemblyReference()
    {
        // The case that matters most and is easiest to miss. This project has no PackageReference,
        // so a reader built only for the modern spelling reports "cannot tell" — for exactly the
        // application whose framework is furthest from any catalog on the machine.
        Assert.Equal("17.1", DeclaredDevExpressVersion.FromProjectFile(LegacyProject));
    }

    [Fact]
    public void ADotNetVersionIsNotADevExpressOne()
    {
        // `Microsoft.Extensions.Logging 9.0.0` is higher than any DevExpress release and sits in
        // the same ItemGroup. Matching on version alone would report this application as 9.0.
        Assert.Equal("23.2", DeclaredDevExpressVersion.FromProjectFile(ModernProject));

        Assert.Null(DeclaredDevExpressVersion.FromProjectFile("""
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup>
            </Project>
            """));
    }

    [Fact]
    public void DeclaringNothingIsNotTheSameAsDeclaringZero()
    {
        // A module getting DevExpress through Directory.Packages.props or a project reference
        // declares nothing here, and null is the honest answer rather than a default.
        Assert.Null(DeclaredDevExpressVersion.FromProjectFile("<Project Sdk=\"Microsoft.NET.Sdk\" />"));
        Assert.Null(DeclaredDevExpressVersion.FromProjectFile(null));
    }

    // --------------------------------------------------- choosing the catalog

    private static string CatalogDirectoryWith(params string[] versions)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xaflogic-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        foreach (var version in versions)
            XafCatalogStore.Save(new XafCatalog { DevExpressVersion = version }, directory);

        return directory;
    }

    [Fact]
    public void PrefersTheCatalogForTheReleaseTheApplicationDeclares()
    {
        var directory = CatalogDirectoryWith("23.2", "26.1");

        try
        {
            Assert.Equal("23.2", XafCatalogStore.LoadFor("23.2.5", directory)?.DevExpressVersion);
            Assert.Equal("26.1", XafCatalogStore.LoadFor("26.1", directory)?.DevExpressVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void FallsBackToWhatExistsRatherThanRefusingToAnswer()
    {
        // Most of the framework is stable across releases, so a catalog two versions out still
        // answers most questions correctly. Withholding it would trade real information for a small
        // error — the obligation it creates is to say so, which the caveat tests below cover.
        var directory = CatalogDirectoryWith("26.1");

        try
        {
            Assert.Equal("26.1", XafCatalogStore.LoadFor("17.1", directory)?.DevExpressVersion);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // --------------------------------------------------- what gets reported

    private static ExtractedProject Project(string? catalogVersion, string? declaredVersion) => new()
    {
        ProjectName = "Legal",
        CatalogVersion = catalogVersion,
        DeclaredDevExpressVersion = declaredVersion,
    };

    [Theory]
    [InlineData(null, "23.2", CatalogTrustLevel.None)]
    [InlineData("26.1", null, CatalogTrustLevel.Undeclared)]
    [InlineData("26.1", "26.1", CatalogTrustLevel.Matched)]
    [InlineData("26.1", "23.2", CatalogTrustLevel.Mismatched)]
    [InlineData("26.1.3", "26.1.5", CatalogTrustLevel.Matched)]
    public void TrustSeparatesCannotTellFromDoesNotFit(
        string? catalogVersion, string? declaredVersion, CatalogTrustLevel expected)
    {
        // "26.1.3 against 26.1.5" is a match: DevExpress ships the framework surface at major.minor
        // and a catalog is generated at that grain, so a patch difference is not a difference.
        Assert.Equal(expected, CatalogTrust.Of(Project(catalogVersion, declaredVersion)));
    }

    [Fact]
    public void OnlyAMismatchInterruptsTheReader()
    {
        // An absent catalog is already explained where the framework section would have been, and a
        // matching one has nothing to say. Repeating either at every claim would be noise.
        Assert.Null(CatalogTrust.Caveat(Project(null, "23.2")));
        Assert.Null(CatalogTrust.Caveat(Project("26.1", "26.1")));

        Assert.NotNull(CatalogTrust.Caveat(Project("26.1", "23.2")));
        Assert.NotNull(CatalogTrust.Caveat(Project("26.1", null)));
    }

    [Fact]
    public void TheCaveatNamesBothVersionsAndSaysHowToFixIt()
    {
        var caveat = CatalogTrust.Caveat(Project("26.1", "23.2"));

        Assert.NotNull(caveat);

        // Both numbers, because "the catalog may not match" without them leaves the reader unable
        // to judge how far off it is — 26.1 against 25.2 and 26.1 against 17.1 are not the same news.
        Assert.Contains("23.2", caveat, StringComparison.Ordinal);
        Assert.Contains("26.1", caveat, StringComparison.Ordinal);
        Assert.Contains("xaflogic catalog build", caveat, StringComparison.Ordinal);
    }

    // --------------------------------------------------- the document itself

    /// <summary>
    /// Reaching the generated page, not just the model that feeds it.
    /// </summary>
    /// <remarks>
    /// A correct <see cref="CatalogTrust"/> that no generator calls would leave every reader exactly
    /// as misinformed as before, which is the shape this defect already had once.
    /// </remarks>
    private static ExtractedProject MismatchedApplication()
    {
        var project = SampleProjects.Extract(SampleProjects.XpoPath);

        project.CatalogVersion = "26.1";
        project.DeclaredDevExpressVersion = "23.2";
        project.FrameworkAlwaysActive = ["DeleteObjectsViewController", "ExportController"];

        return project;
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void TheGeneratedDocumentSaysTheCatalogIsFromAnotherRelease(string language)
    {
        var markdown = string.Join("\n", new MarkdownDocumentationGenerator(language)
            .GenerateSections(MismatchedApplication())
            .Select(section => section.Content));

        Assert.Contains("23.2", markdown, StringComparison.Ordinal);
        Assert.Contains("26.1", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAgentContextDeclaresTheMismatchInItsHeader()
    {
        // AGENTS.md is read by something that will act on it and has no way to doubt it, so the
        // qualification belongs at the top rather than only beside the section it affects.
        var context = new AgentContextGenerator().GenerateIndex(MismatchedApplication(), []);

        Assert.Contains("23.2", context, StringComparison.Ordinal);
        Assert.Contains("26.1", context, StringComparison.Ordinal);
    }

    [Fact]
    public void AMatchingCatalogAddsNoNoiseToTheDocument()
    {
        var project = MismatchedApplication();
        project.DeclaredDevExpressVersion = "26.1";

        var markdown = string.Join("\n", new MarkdownDocumentationGenerator("en")
            .GenerateSections(project)
            .Select(section => section.Content));

        Assert.DoesNotContain("closest answer available", markdown, StringComparison.Ordinal);
    }
}
