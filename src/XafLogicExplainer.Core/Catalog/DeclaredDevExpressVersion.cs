using System.Text.RegularExpressions;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Catalog;

/// <summary>
/// Which DevExpress version an application says it is written against.
/// </summary>
/// <remarks>
/// Read from the project file, so it costs nothing and needs no DevExpress installation — it is the
/// application's own declaration rather than a probe of the machine.
/// <para>
/// It exists to answer a question the catalog could not previously ask: <em>does the catalog we are
/// about to consult describe the framework this application actually uses?</em> A controller can be
/// introduced, retargeted or removed between releases, so answering "these 30 controllers load onto
/// this screen" from a 26.1 catalog for a 23.2 application is not a close-enough answer, it is a
/// confident wrong one.
/// </para>
/// <para>
/// <strong>Two spellings, because XAF applications outlive project formats.</strong> An SDK-style
/// project names its version in <c>PackageReference</c>. A project from before XAF moved to NuGet
/// names it in the assembly itself — <c>DevExpress.ExpressApp.Xpo.v17.1</c> — and has no
/// <c>PackageReference</c> at all. Reading only the first would return "cannot tell" for exactly
/// the old applications where a version mismatch is largest and matters most.
/// </para>
/// </remarks>
public static class DeclaredDevExpressVersion
{
    /// <summary>
    /// <c>&lt;PackageReference Include="DevExpress.ExpressApp" Version="23.2.5" /&gt;</c>.
    /// </summary>
    private static readonly Regex PackageForm = new(
        """Include\s*=\s*"DevExpress\.[^"]*"[^>]*?Version\s*=\s*"(?<major>\d+)\.(?<minor>\d+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <c>&lt;Reference Include="DevExpress.ExpressApp.Xpo.v17.1, Version=17.1.4.0, ..." /&gt;</c>,
    /// and the short form with no version attribute at all. The <c>vNN.N</c> in the assembly name
    /// is the version in both.
    /// </summary>
    private static readonly Regex AssemblyForm = new(
        """Include\s*=\s*"DevExpress\.[^"]*?\.v(?<major>\d+)\.(?<minor>\d+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MajorMinorPrefix = new(
        @"^(?<major>\d+)\.(?<minor>\d+)", RegexOptions.Compiled);

    /// <summary>
    /// The <c>major.minor</c> version a project file declares, or null when it names no DevExpress
    /// reference in either form.
    /// </summary>
    /// <remarks>
    /// Reduced to <c>major.minor</c> because that is the grain DevExpress ships and the grain a
    /// catalog is generated at: <c>23.2.5</c> and <c>23.2.7</c> are the same framework surface.
    /// <para>
    /// Null is an ordinary outcome, not a failure. A module that gets DevExpress through a shared
    /// <c>Directory.Packages.props</c> or through a project reference declares nothing here, and
    /// "I cannot tell" is the honest answer — which is why it stays distinct from a mismatch
    /// instead of being folded into one.
    /// </para>
    /// </remarks>
    /// <param name="projectFileContent">Raw text of a <c>.csproj</c>.</param>
    public static string? FromProjectFile(string? projectFileContent)
    {
        if (string.IsNullOrWhiteSpace(projectFileContent))
            return null;

        (int Major, int Minor)? highest = null;

        foreach (Match match in PackageForm.Matches(projectFileContent))
            highest = Higher(highest, match);

        foreach (Match match in AssemblyForm.Matches(projectFileContent))
            highest = Higher(highest, match);

        return Format(highest);
    }

    /// <summary>
    /// The version declared by an already-extracted project.
    /// </summary>
    /// <remarks>
    /// Prefers what was read from the project file, falling back to the package reference list for
    /// projects assembled without one — which is every project built by hand in a test.
    /// </remarks>
    /// <param name="project">An extracted project.</param>
    public static string? Of(ExtractedProject project) =>
        project.DeclaredDevExpressVersion ?? Of(project.PackageReferences);

    /// <summary>
    /// The version declared by package reference lines of the form <c>Package.Name 23.2.5</c>.
    /// </summary>
    /// <param name="packageReferences">Extracted package reference lines.</param>
    public static string? Of(IEnumerable<string> packageReferences)
    {
        (int Major, int Minor)? highest = null;

        foreach (var reference in packageReferences)
        {
            var trimmed = reference.Trim();

            if (!trimmed.StartsWith("DevExpress.", StringComparison.OrdinalIgnoreCase))
                continue;

            var space = trimmed.LastIndexOf(' ');
            if (space < 0)
                continue;

            var match = MajorMinorPrefix.Match(trimmed[(space + 1)..]);
            if (match.Success)
                highest = Higher(highest, match);
        }

        return Format(highest);
    }

    /// <summary>
    /// Reduces any version string to <c>major.minor</c>, for comparing a catalog against a
    /// declaration.
    /// </summary>
    /// <param name="version">A version as written anywhere, e.g. <c>26.1</c> or <c>26.1.3</c>.</param>
    public static string? MajorMinor(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return null;

        var match = MajorMinorPrefix.Match(version.Trim());

        return match.Success
            ? $"{match.Groups["major"].Value}.{match.Groups["minor"].Value}"
            : null;
    }

    /// <summary>
    /// Keeps the higher of two versions.
    /// </summary>
    /// <remarks>
    /// References disagreeing means the project is already in a state that will not restore
    /// cleanly. Any choice is arguable there; taking the highest at least compares against
    /// something the project genuinely names.
    /// </remarks>
    private static (int Major, int Minor)? Higher((int Major, int Minor)? current, Match match)
    {
        var candidate = (
            Major: int.Parse(match.Groups["major"].Value),
            Minor: int.Parse(match.Groups["minor"].Value));

        if (current is null)
            return candidate;

        return candidate.Major > current.Value.Major
               || (candidate.Major == current.Value.Major && candidate.Minor > current.Value.Minor)
            ? candidate
            : current;
    }

    private static string? Format((int Major, int Minor)? version) =>
        version is null ? null : $"{version.Value.Major}.{version.Value.Minor}";
}
