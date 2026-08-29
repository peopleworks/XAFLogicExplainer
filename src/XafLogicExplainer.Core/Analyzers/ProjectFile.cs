using System.Text.RegularExpressions;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// The project file a module directory belongs to, and the projects it references.
/// </summary>
public static class ProjectFile
{
    /// <summary>
    /// <c>&lt;ProjectReference Include="..\Shared\Shared.csproj" /&gt;</c>.
    /// </summary>
    private static readonly Regex ReferenceForm = new(
        """<ProjectReference\s[^>]*?Include\s*=\s*"(?<path>[^"]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The project file a directory is named for, when several sit side by side.
    /// </summary>
    /// <remarks>
    /// A module folder holding exactly one <c>.csproj</c> is the common case and any rule agrees
    /// about it. Real solutions do not stop there: one holds
    /// <c>PWPresupuesto.Module.csproj</c> beside <c>PWPresupuesto.Module.Net10.csproj</c> from a
    /// framework migration, another holds a hand-made <c>"pwLegalOffice - Backup.Module.csproj"</c>.
    /// <para>
    /// Taking the first entry <see cref="Directory.GetFiles(string, string, SearchOption)"/>
    /// returned made the answer depend on how the file system chose to order the directory, so two
    /// machines could describe the same application differently and neither would say why. The
    /// convention every .NET project follows decides it instead: the file named after the folder is
    /// the project, and a backup or a migration candidate beside it is not.
    /// </para>
    /// <para>
    /// Ordinal ordering is the fallback rather than the enumeration order, so the case this rule
    /// does not cover is at least the same everywhere.
    /// </para>
    /// </remarks>
    public static string? Main(string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return null;

        var candidates = Directory
            .GetFiles(projectPath, "*.csproj", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count <= 1)
            return candidates.FirstOrDefault();

        var folderName = new DirectoryInfo(projectPath).Name;

        return candidates.FirstOrDefault(path =>
                   Path.GetFileNameWithoutExtension(path)
                       .Equals(folderName, StringComparison.OrdinalIgnoreCase))
               ?? candidates[0];
    }

    /// <summary>
    /// The directories of the projects this one references, following them transitively.
    /// </summary>
    /// <remarks>
    /// A module whose entities derive from a base declared in a referenced project has to be able
    /// to reach that declaration, or every one of its business objects is dropped — silently, and
    /// in the layout where the shared project happens to sit beside the module, replaced by the
    /// abstract base itself. A developer with a shared framework library is exactly who this tool
    /// is for.
    /// <para>
    /// Transitive, because a base is as likely to be two hops away as one: an application
    /// references its own framework project, which references the audit primitives. Bounded by
    /// <paramref name="maxDepth"/> and by a visited set, so a reference cycle — which MSBuild
    /// forbids but a hand-edited project file can still contain — terminates rather than hanging
    /// the extraction.
    /// </para>
    /// <para>
    /// Read as text, like everything else here: no build, no NuGet restore, no MSBuild evaluation.
    /// A reference whose path does not resolve to a directory on this machine is skipped rather
    /// than reported, because the honest reading of it is that the source is not here.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> ReferencedDirectories(string projectPath, int maxDepth = 3)
    {
        var found = new List<string>();

        if (!Directory.Exists(projectPath))
            return found;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(projectPath),
        };

        var frontier = new List<string> { Path.GetFullPath(projectPath) };

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var next = new List<string>();

            foreach (var directory in frontier)
            {
                foreach (var referenced in ReferencesOf(directory))
                {
                    if (!visited.Add(referenced))
                        continue;

                    found.Add(referenced);
                    next.Add(referenced);
                }
            }

            frontier = next;
        }

        // Ordinal, so two machines that walked the same references in a different order still
        // parse the same files in the same sequence -- the reason the file list is ordered too.
        found.Sort(StringComparer.Ordinal);

        return found;
    }

    /// <summary>
    /// The directories named by one project file's <c>ProjectReference</c> items.
    /// </summary>
    private static List<string> ReferencesOf(string projectPath)
    {
        var directories = new List<string>();
        var projectFile = Main(projectPath);

        if (projectFile is null)
            return directories;

        string content;

        try
        {
            content = File.ReadAllText(projectFile);
        }
        catch (IOException)
        {
            return directories;
        }
        catch (UnauthorizedAccessException)
        {
            return directories;
        }

        var from = Path.GetDirectoryName(Path.GetFullPath(projectFile));

        if (from is null)
            return directories;

        foreach (Match match in ReferenceForm.Matches(content))
        {
            // A project file always writes a backslash, whichever platform reads it. On Linux that
            // is a legal filename character rather than a separator, so a path left as written
            // resolves to one directory whose name contains the whole relative path and is never
            // found -- and the failure would be invisible on the machine it was written on.
            var relative = match.Groups["path"].Value
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);

            string resolved;

            try
            {
                resolved = Path.GetFullPath(Path.Combine(from, relative));
            }
            catch (ArgumentException)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(resolved);

            if (directory is not null && Directory.Exists(directory))
                directories.Add(directory);
        }

        return directories;
    }
}
