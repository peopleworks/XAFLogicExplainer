using System.Text.Json;
using System.Text.Json.Serialization;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Hashing;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Mcp;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That an edit which changes the extraction is noticed by everything that decides whether to
/// extract again.
/// </summary>
/// <remarks>
/// The CLI hash, the MCP cache fingerprint and <c>watch</c> each listed the files they covered by hand,
/// and each missed part of what the extraction reads: a controller in the Blazor.Server project, a
/// report layout at the solution root, a base in a referenced project. Every miss meant documentation
/// that was wrong and said it was current.
/// <para>
/// One test per kind of file, each asserting both fingerprints, in a throwaway solution so an edit can
/// never touch a shared fixture. Then one test that fails the day an analyzer cites a file the roster
/// does not list, which is how the next place the extraction learns to read gets covered too.
/// </para>
/// </remarks>
public class ChangeDetectionCoverageTests
{
    private const string Layout = """<?xml version="1.0" encoding="utf-8"?><XtraReportsLayoutSerializer Name="Summary" />""";

    [Fact]
    public Task ALayoutAtTheSolutionRootIsCovered() => AssertNoticed(
        arrange: s => s.Write("solution/Reporting/RegionSummary.repx", Layout),
        edit: s => File.Delete(s.PathOf("solution/Reporting/RegionSummary.repx")));

    [Fact]
    public Task ALayoutInsideTheModuleIsCovered() => AssertNoticed(
        arrange: s => s.Write("solution/App.Module/Reports/Statement.repx", Layout),
        edit: s => File.AppendAllText(s.PathOf("solution/App.Module/Reports/Statement.repx"), "\n<!-- edited -->"));

    [Fact]
    public Task AControllerInASiblingProjectIsCovered() => AssertNoticed(
        arrange: s => s.Write("solution/App.Blazor.Server/Controllers/FirstController.cs",
            "namespace App.Blazor; public class FirstController { }"),
        edit: s => s.Write("solution/App.Blazor.Server/Controllers/SecondController.cs",
            "namespace App.Blazor; public class SecondController { }"));

    [Fact]
    public Task AModelFileInASiblingProjectIsCovered() => AssertNoticed(
        arrange: s => s.Write("solution/App.Blazor.Server/Startup.cs", "namespace App.Blazor; public class Startup { }"),
        edit: s => s.Write("solution/App.Blazor.Server/Model.xafml", "<Application />"));

    [Fact]
    public Task SourceInAReferencedProjectIsCovered() => AssertNoticed(
        arrange: s =>
        {
            s.WriteLibrary();
            s.Write("solution/App.Module/App.Module.csproj", ReferenceToLibrary);
        },
        edit: s => File.AppendAllText(s.PathOf("library/Shared/AuditedEntity.cs"), "\n// a property arrives on the shared base"));

    [Fact]
    public Task SourceASiblingProjectReferencesIsCovered() => AssertNoticed(
        arrange: s =>
        {
            s.WriteLibrary();
            s.Write("solution/App.Blazor.Server/App.Blazor.Server.csproj", ReferenceToLibrary);
            s.Write("solution/App.Blazor.Server/Startup.cs", "namespace App.Blazor; public class Startup { }");
        },
        edit: s => File.AppendAllText(s.PathOf("library/Shared/AuditedEntity.cs"), "\n// a property arrives on the shared base"));

    [Fact]
    public Task TheModuleProjectFileIsCovered() => AssertNoticed(
        arrange: _ => { },
        edit: s => s.Write("solution/App.Module/App.Module.csproj", ReferenceToLibrary));

    [Fact]
    public Task AnEditOutsideAnythingTheExtractionReadsIsNotNoticed() => AssertNoticed(
        arrange: _ => { },
        edit: s => s.Write("solution/App.Module/bin/Debug/Generated.cs", "namespace App; public class Generated { }"),
        expectNoticed: false);

    /// <summary>
    /// Every project in every fixture, so the guard holds on every layout the suite already knows.
    /// </summary>
    /// <remarks>
    /// One fact that walks them all rather than a theory case each: the README counts what the runner
    /// reports, and a list of what failed where reads better than thirty separate red lines.
    /// </remarks>
    private static IEnumerable<string> FixtureProjects() =>
        Directory.GetDirectories(FixturesRoot).Order(StringComparer.Ordinal)
            .SelectMany(solution => Directory.GetDirectories(solution).Order(StringComparer.Ordinal))
            .Where(project => Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories).Any());

    [Fact]
    public void EveryFileTheExtractionCitesIsInTheRoster()
    {
        var missing = new List<string>();

        foreach (var project in FixtureProjects())
        {
            var roster = SourceRoster.Files(project).ToHashSet(StringComparer.OrdinalIgnoreCase);

            missing.AddRange(CitedFiles(new LogicExtractor().ExtractFromSourceDirectory(project))
                .Select(Path.GetFullPath)
                .Where(file => !roster.Contains(file))
                .Select(file => $"{Path.GetRelativePath(FixturesRoot, project)} cites {file}"));
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void TheGuardHasSomethingToCheck()
    {
        // A guard over a model that cites nothing would pass for the wrong reason. The demo cites
        // entities, controllers in both projects and editors in the platform project.
        var cited = CitedFiles(new LogicExtractor().ExtractFromSourceDirectory(SampleProjects.DemoPath));

        Assert.Contains(cited, file => file.Contains("PharmacyDemo.Blazor.Server", StringComparison.Ordinal));
        Assert.Contains(cited, file => file.Contains("PharmacyDemo.Module", StringComparison.Ordinal));
    }

    [Fact]
    public void TheWatchedDirectoriesSpanTheRoster()
    {
        var unwatched = new List<string>();

        foreach (var project in FixtureProjects())
        {
            var roots = SourceRoster.WatchRoots(project);

            unwatched.AddRange(SourceRoster.Files(project)
                .Where(file => !roots.Any(root => file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                .Select(file => $"{Path.GetRelativePath(FixturesRoot, project)} leaves {file} unwatched"));
        }

        Assert.Empty(unwatched);
    }

    private const string ReferenceToLibrary =
        """<Project><ItemGroup><ProjectReference Include="..\..\library\Shared\Shared.csproj" /></ItemGroup></Project>""";

    private static string FixturesRoot =>
        Path.GetDirectoryName(Path.GetDirectoryName(SampleProjects.XpoPath))!;

    private static async Task AssertNoticed(Action<TempSolution> arrange, Action<TempSolution> edit, bool expectNoticed = true)
    {
        using var solution = new TempSolution();
        arrange(solution);

        var ct = TestContext.Current.CancellationToken;
        var hasher = new ProjectHashCalculator();
        var context = new XafProjectContext([new XafProjectSource { Name = "App", Path = solution.Module }]);

        var hashBefore = hasher.ComputeHash(solution.Module);
        var cachedBefore = await context.GetAsync(null, ct);

        edit(solution);

        var hashAfter = hasher.ComputeHash(solution.Module);
        var cachedAfter = await context.GetAsync(null, ct);

        if (expectNoticed)
        {
            Assert.NotEqual(hashBefore, hashAfter);
            Assert.NotSame(cachedBefore, cachedAfter);
        }
        else
        {
            Assert.Equal(hashBefore, hashAfter);
            Assert.Same(cachedBefore, cachedAfter);
        }
    }

    /// <summary>
    /// Every file path the extraction result mentions, wherever in the model it sits.
    /// </summary>
    /// <remarks>
    /// Read off the serialized model rather than walked type by type, so a model that grows a
    /// <c>FilePath</c> next year is checked without anyone remembering to add it here.
    /// </remarks>
    private static List<string> CitedFiles(ExtractedProject project)
    {
        var json = JsonSerializer.SerializeToElement(project,
            new JsonSerializerOptions { ReferenceHandler = ReferenceHandler.IgnoreCycles, MaxDepth = 512 });

        var found = new List<string>();
        Collect(json, found);
        return found.Where(file => !string.IsNullOrWhiteSpace(file)).Distinct(StringComparer.Ordinal).ToList();

        static void Collect(JsonElement element, List<string> found)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name == "FilePath" && property.Value.ValueKind == JsonValueKind.String)
                            found.Add(property.Value.GetString()!);
                        else if (property.Name == "SourceFiles" && property.Value.ValueKind == JsonValueKind.Array)
                            found.AddRange(property.Value.EnumerateArray()
                                .Where(item => item.ValueKind == JsonValueKind.String)
                                .Select(item => item.GetString()!));
                        else
                            Collect(property.Value, found);
                    }
                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                        Collect(item, found);
                    break;
            }
        }
    }

    /// <summary>
    /// A solution folder with one module in it, and a library beside the solution when a test needs one.
    /// </summary>
    private sealed class TempSolution : IDisposable
    {
        public TempSolution()
        {
            Root = Path.Combine(Path.GetTempPath(), $"xle-roster-{Guid.NewGuid():N}");
            Write("solution/App.Module/App.Module.csproj", "<Project />");
            Write("solution/App.Module/BusinessObjects/Invoice.cs",
                "namespace App; public class Invoice { public decimal Total { get; set; } }");
        }

        public string Root { get; }

        public string Module => PathOf("solution/App.Module");

        public string PathOf(string relative) =>
            Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

        public void Write(string relative, string content)
        {
            var path = PathOf(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void WriteLibrary()
        {
            Write("library/Shared/Shared.csproj", "<Project />");
            Write("library/Shared/AuditedEntity.cs",
                "namespace Shared; public abstract class AuditedEntity { public DateTime CreatedOn { get; set; } }");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
                // A scanner holding a file open must not fail the test that already passed.
            }
        }
    }
}
