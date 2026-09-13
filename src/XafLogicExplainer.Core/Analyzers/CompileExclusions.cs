using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// The source files a project file takes out of its own build with <c>&lt;Compile Remove&gt;</c>.
/// </summary>
/// <remarks>
/// A file can sit in the project folder and not be part of the application: a draft set aside, a copy
/// kept under another namespace, a backup. Analyzers find source by folder, so every class in such a
/// file was listed as the application's own — and the index calls its list complete (#86). The largest
/// of six real applications had two: one listed in a navigation group, one under the name of a class
/// that is compiled, which is how the explain page came to meet two classes of one name.
/// <para>
/// Read as text, like the rest of the project file, without evaluating it. Every limit that follows is
/// resolved toward reading a file rather than hiding one, because a class hidden by mistake is the more
/// damaging wrong answer of the two:
/// </para>
/// <list type="bullet">
///   <item>Only an SDK-style project file is read. Its default glob compiles every source file in the
///   folder, so its removals are the whole story. A pre-SDK project file compiles only what it lists,
///   but in a folder mid-migration the folder-named pre-SDK file is often the abandoned one: one real
///   application builds from <c>.Net10.csproj</c> while its old file misses six live files.</item>
///   <item>A removal under a <c>Condition</c>, on the item or on its <c>ItemGroup</c>, is ignored. It
///   applies to one configuration or framework, and the others still compile the file.</item>
///   <item>A pattern built from a property or an item list — <c>$(…)</c>, <c>@(…)</c> — cannot be known
///   without evaluating the project, and is ignored.</item>
/// </list>
/// <para>
/// Only the project file of the directory being analyzed decides, never one above it. A module folder
/// with no project file of its own can sit inside another project's tree: this repository's test
/// project removes every fixture from its own build, and four fixtures have no project file.
/// </para>
/// </remarks>
public sealed class CompileExclusions
{
    private static readonly CompileExclusions Nothing = new(string.Empty, []);

    private readonly string _projectDirectory;
    private readonly Regex[] _removed;

    private CompileExclusions(string projectDirectory, Regex[] removed)
    {
        _projectDirectory = projectDirectory;
        _removed = removed;
    }

    /// <summary>
    /// Reads the removals of the project file in <paramref name="projectDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Built once per analyzer run and asked once per file, so the project file is read once rather
    /// than once for every source file beside it.
    /// </remarks>
    /// <param name="projectDirectory">The directory being analyzed.</param>
    /// <returns>An instance that excludes nothing when there is no SDK-style project file to read.</returns>
    public static CompileExclusions For(string projectDirectory)
    {
        var projectFile = ProjectFile.Main(projectDirectory);

        if (projectFile is null)
            return Nothing;

        XElement? project;

        try
        {
            project = XDocument.Load(projectFile).Root;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            return Nothing;
        }

        if (project is null || !IsSdkStyle(project))
            return Nothing;

        var removed = project.Elements()
            .Where(element => element.Name.LocalName == "ItemGroup" && element.Attribute("Condition") is null)
            .SelectMany(group => group.Elements())
            .Where(item => item.Name.LocalName == "Compile" && item.Attribute("Condition") is null)
            .Select(item => item.Attribute("Remove")?.Value)
            .OfType<string>()
            .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(pattern => !pattern.Contains("$(", StringComparison.Ordinal)
                              && !pattern.Contains("@(", StringComparison.Ordinal)
                              && !pattern.Contains("%(", StringComparison.Ordinal))
            .Select(ToRegex)
            .ToArray();

        return removed.Length == 0
            ? Nothing
            : new CompileExclusions(Normalize(Path.GetFullPath(projectDirectory)), removed);
    }

    /// <summary>
    /// Whether the project file removes this source file from the build.
    /// </summary>
    /// <param name="file">A source file under the analyzed directory.</param>
    public bool Excludes(string file)
    {
        if (_removed.Length == 0)
            return false;

        var path = Normalize(Path.GetFullPath(file));

        // Case ignored, as MSBuild ignores it on Windows, where these applications are written.
        if (!path.StartsWith(_projectDirectory + "/", StringComparison.OrdinalIgnoreCase))
            return false;

        var relative = path[(_projectDirectory.Length + 1)..];

        return _removed.Any(pattern => pattern.IsMatch(relative));
    }

    private static bool IsSdkStyle(XElement project) =>
        project.Attribute("Sdk") is not null
        || project.Elements().Any(element =>
            element.Name.LocalName == "Sdk"
            || (element.Name.LocalName == "Import" && element.Attribute("Sdk") is not null));

    /// <summary>
    /// An MSBuild item pattern as a regular expression over a path relative to the project folder.
    /// </summary>
    /// <remarks>
    /// <c>**</c> spans directories, <c>*</c> and <c>?</c> stay within one. <c>Drafts\**</c> is everything
    /// under <c>Drafts</c>; <c>**\*.Backup.cs</c> is a backup at any depth, the top folder included.
    /// </remarks>
    private static Regex ToRegex(string pattern)
    {
        var glob = pattern.Replace('\\', '/');

        if (glob.StartsWith("./", StringComparison.Ordinal))
            glob = glob[2..];

        var expression = new StringBuilder("^");

        for (var i = 0; i < glob.Length; i++)
        {
            if (glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                var spansDirectories = i + 2 < glob.Length && glob[i + 2] == '/';
                expression.Append(spansDirectories ? "(?:.*/)?" : ".*");
                i += spansDirectories ? 2 : 1;
            }
            else if (glob[i] == '*')
            {
                expression.Append("[^/]*");
            }
            else if (glob[i] == '?')
            {
                expression.Append("[^/]");
            }
            else
            {
                expression.Append(Regex.Escape(glob[i].ToString()));
            }
        }

        return new Regex(expression.Append('$').ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').TrimEnd('/');
}
