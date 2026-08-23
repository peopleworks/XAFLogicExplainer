using System.Text.RegularExpressions;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Resolves the MSBuild properties a project file defines and uses in the same file.
/// </summary>
/// <remarks>
/// The version of a package is not always written where it is used. DevExpress's own current
/// template declares it once and refers to it everywhere:
/// <code>
/// &lt;DevExpressVersion&gt;25.2.7&lt;/DevExpressVersion&gt;
/// ...
/// &lt;PackageReference Include="DevExpress.ExpressApp" Version="$(DevExpressVersion)" /&gt;
/// </code>
/// Read literally, that project declares a version of <c>$(DevExpressVersion)</c> — which is not a
/// version, so anything matching on digits finds nothing and reports an application that names no
/// framework at all. This is not an exotic spelling to be tolerant of; it is what a project
/// generated today looks like.
/// <para>
/// Deliberately not an MSBuild evaluation. There are no conditions, no imports, no well-known
/// properties and no nesting here — one pass over the literal properties this file sets, which is
/// what the pattern above needs and nothing more. A property defined in a
/// <c>Directory.Build.props</c> beside the project stays unresolved, and unresolved is reported as
/// "cannot tell" rather than guessed at.
/// </para>
/// </remarks>
public static class ProjectFileProperties
{
    /// <summary>
    /// A property assignment: an element whose content has no markup of its own.
    /// </summary>
    private static readonly Regex Assignment = new(
        @"<(?<name>[A-Za-z_][A-Za-z0-9_.-]*)\s*>(?<value>[^<>]*)</\k<name>\s*>",
        RegexOptions.Compiled);

    private static readonly Regex Usage = new(
        @"\$\((?<name>[A-Za-z_][A-Za-z0-9_.-]*)\)", RegexOptions.Compiled);

    /// <summary>
    /// The same text with every <c>$(Property)</c> the file itself defines replaced by its value.
    /// </summary>
    /// <remarks>
    /// References to properties this file does not define are left exactly as written, so a caller
    /// can still tell that a value was never resolved instead of receiving an empty string that
    /// looks like an answer.
    /// </remarks>
    /// <param name="projectFileContent">Raw text of a project file, or null.</param>
    public static string Expand(string? projectFileContent)
    {
        if (string.IsNullOrEmpty(projectFileContent))
            return string.Empty;

        if (!projectFileContent.Contains("$(", StringComparison.Ordinal))
            return projectFileContent;

        var properties = Collect(projectFileContent);

        if (properties.Count == 0)
            return projectFileContent;

        return Usage.Replace(projectFileContent, match =>
            properties.TryGetValue(match.Groups["name"].Value, out var value)
                ? value
                : match.Value);
    }

    /// <summary>
    /// The literal properties a project file sets, last assignment winning.
    /// </summary>
    /// <param name="projectFileContent">Raw text of a project file.</param>
    public static Dictionary<string, string> Collect(string projectFileContent)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in Assignment.Matches(projectFileContent))
        {
            var value = match.Groups["value"].Value.Trim();

            // A property whose value is itself a reference resolves to nothing useful in one pass,
            // and guessing across passes is how a simple reader starts pretending to be MSBuild.
            if (value.Contains("$(", StringComparison.Ordinal))
                continue;

            properties[match.Groups["name"].Value] = value;
        }

        return properties;
    }
}
