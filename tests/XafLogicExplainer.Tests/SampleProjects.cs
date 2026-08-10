using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The synthetic XAF applications the tests analyze.
/// </summary>
/// <remarks>
/// The fixtures are XAF source that never compiles here — they reference DevExpress assemblies
/// that are not installed. That is the point: extraction is syntax analysis, so a test run needs
/// no DevExpress licence and no private feed, and CI can verify the engine on a public runner.
/// <para>
/// Extraction runs once per fixture for the whole test session. It parses every file in the
/// project, which is fast but not free, and no test mutates the result.
/// </para>
/// </remarks>
internal static class SampleProjects
{
    // Each fixture sits inside its own solution folder, because extraction deliberately looks at
    // sibling directories -- that is how it finds the Blazor.Server and Win projects that
    // accompany a real XAF module. Fixtures side by side under one parent made the XPO project
    // absorb the EF Core entities, which is the feature working correctly on an unrealistic layout.

    /// <summary>Path to the XPO fixture module.</summary>
    public static string XpoPath => Path.Combine(FixturesRoot, "XpoSolution", "SampleApp.Module");

    /// <summary>Path to the EF Core fixture module.</summary>
    public static string EfCorePath => Path.Combine(FixturesRoot, "EfCoreSolution", "SampleEf.Module");

    /// <summary>
    /// Locates the fixtures in the source tree by walking up from the test assembly.
    /// </summary>
    /// <remarks>
    /// Not <c>AppContext.BaseDirectory</c>, and not a copy in the output directory: extraction
    /// skips anything under <c>bin</c> or <c>obj</c>, so fixtures copied to
    /// <c>bin/Debug/net10.0/</c> would be correctly ignored and every extraction test would see an
    /// empty project. Reading them from source is also closer to how the tool is really used.
    /// <para>
    /// A compile-time <c>[CallerFilePath]</c> would be tidier but breaks under
    /// <c>ContinuousIntegrationBuild</c>, which rewrites source paths to a deterministic root that
    /// does not exist on disk.
    /// </para>
    /// </remarks>
    private static string FixturesRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "Fixtures");
                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                $"No 'Fixtures' directory found above '{AppContext.BaseDirectory}'. " +
                "Tests read the fixtures from the source tree; run them from a working copy.");
        }
    }

    private static readonly Lazy<ExtractedProject> LazyXpo = new(() => Extract(XpoPath));
    private static readonly Lazy<ExtractedProject> LazyEfCore = new(() => Extract(EfCorePath));

    /// <summary>The XPO sample: Customer, Order, OrderLine, one controller, seed data, xafml.</summary>
    public static ExtractedProject Xpo => LazyXpo.Value;

    /// <summary>The EF Core sample: Product and Category.</summary>
    public static ExtractedProject EfCore => LazyEfCore.Value;

    /// <summary>Finds one entity by name, failing the test clearly when it is missing.</summary>
    public static ExtractedEntity Entity(this ExtractedProject project, string className) =>
        project.Entities.FirstOrDefault(e => e.ClassName == className)
        ?? throw new InvalidOperationException(
            $"The fixture has no entity '{className}'. Extracted: " +
            string.Join(", ", project.Entities.Select(e => e.ClassName)));

    /// <summary>Finds one property by name, failing the test clearly when it is missing.</summary>
    public static ExtractedProperty Property(this ExtractedEntity entity, string name) =>
        entity.Properties.FirstOrDefault(p => p.Name == name)
        ?? throw new InvalidOperationException(
            $"'{entity.ClassName}' has no property '{name}'. Extracted: " +
            string.Join(", ", entity.Properties.Select(p => p.Name)));

    private static ExtractedProject Extract(string path)
    {
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(
                $"Fixture not found at '{path}'. It should have been copied to the output " +
                "directory by the None/CopyToOutputDirectory item in the test project.");
        }

        var options = new ExtractionOptions
        {
            BaseTypeNames = ["XPCustomObject", "BaseObject", "XPObject", "XPLiteObject", "PermissionPolicyUser"],
            IncludeSourceCode = true,
            IncludeMethodBodies = true,
            IncludeComments = true,
            LanguageCode = "en",
        };

        return new LogicExtractor().ExtractFromSourceDirectory(path, options);
    }
}
