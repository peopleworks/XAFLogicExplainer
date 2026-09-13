namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Every file the extraction can read for one project, found by the same rules the analyzers use.
/// </summary>
/// <remarks>
/// Three things decide whether a project is read again: the CLI's change hash, the MCP server's cache
/// fingerprint, and <c>watch</c>. Each used to rebuild the list of files on its own, and each covered
/// part of it. A controller added in the Blazor.Server project, a report layout at the solution root,
/// a property on a base in a referenced project: every one changed what the extraction produced while
/// all three reported nothing to do. That is documentation that is wrong and says it is current.
/// <para>
/// So the list is decided here, once, out of the discovery the extraction itself performs: the module,
/// the siblings <see cref="SiblingDirectories"/> finds, the projects any of them reference, the model
/// files beside them, and every layout <see cref="ReportLayoutReader.RepxFiles"/> finds. The extraction
/// calls the same functions, so the two cannot drift apart without one of them being edited.
/// </para>
/// <para>
/// It errs wide. It lists every <c>.cs</c> in a directory where an analyzer may read only some of them,
/// and includes siblings and references whether or not those options are switched on. Covering more
/// costs one unnecessary run; covering less is the defect this class exists to end.
/// </para>
/// </remarks>
public static class SourceRoster
{
    private static readonly string[] ReadExtensions = [".cs", ".xafml", ".csproj", ".repx"];

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Every file the extraction of <paramref name="projectPath"/> can read, as full paths in ordinal order.
    /// </summary>
    /// <param name="projectPath">The module directory the extraction is pointed at.</param>
    /// <returns>An empty list when the directory does not exist.</returns>
    public static IReadOnlyList<string> Files(string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return [];

        var module = Path.GetFullPath(projectPath);
        var files = new HashSet<string>(PathComparer);

        foreach (var directory in CodeDirectories(module))
        {
            Add(files, directory, "*.cs", SearchOption.AllDirectories);

            // A project file decides what else is read: its references, its declared framework and
            // DevExpress version. Editing one changes the extraction without changing a line of C#.
            Add(files, directory, "*.csproj", SearchOption.TopDirectoryOnly);
        }

        // Model files anywhere under the module, and at the top of every directory beside it, which is
        // where ModelAnalyzer looks for a platform project's Model.xafml.
        Add(files, module, "*.xafml", SearchOption.AllDirectories);

        var parent = Directory.GetParent(module)?.FullName;
        if (parent is not null)
        {
            foreach (var directory in Subdirectories(parent))
                Add(files, directory, "*.xafml", SearchOption.TopDirectoryOnly);
        }

        try
        {
            foreach (var layout in ReportLayoutReader.RepxFiles(module))
                files.Add(Path.GetFullPath(layout));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A tree that cannot be listed cannot be read by the extraction either.
        }

        var ordered = files.ToList();
        ordered.Sort(StringComparer.Ordinal);
        return ordered;
    }

    /// <summary>
    /// Directories that, watched recursively, see a change to every file <see cref="Files"/> lists.
    /// </summary>
    /// <remarks>
    /// The directories new files can appear in: the module, its siblings, what they reference, and
    /// wherever the report search looks. Nested directories are collapsed into the outermost one, so
    /// when the report search climbs to the solution folder that single root covers the module and
    /// every sibling. Anything already listed outside them gets its own directory as a root, which is
    /// what makes the summary line true by construction.
    /// </remarks>
    /// <param name="projectPath">The module directory the extraction is pointed at.</param>
    public static IReadOnlyList<string> WatchRoots(string projectPath)
    {
        if (!Directory.Exists(projectPath))
            return [];

        var module = Path.GetFullPath(projectPath);
        var candidates = CodeDirectories(module).ToList();
        candidates.Add(ReportLayoutReader.RepxRoot(module));

        var roots = Collapse(candidates);

        var outside = Files(module)
            .Where(file => !roots.Any(root => IsUnder(file, root)))
            .Select(file => Path.GetDirectoryName(file)!)
            .ToList();

        return outside.Count == 0 ? roots : Collapse(roots.Concat(outside));
    }

    private static List<string> Collapse(IEnumerable<string> directories)
    {
        var roots = new List<string>();
        foreach (var directory in directories
                     .Select(d => Path.TrimEndingDirectorySeparator(Path.GetFullPath(d)))
                     .Distinct(PathComparer)
                     .OrderBy(d => d.Length))
        {
            if (!roots.Any(root => IsUnder(directory, root)))
                roots.Add(directory);
        }

        roots.Sort(StringComparer.Ordinal);
        return roots;
    }

    /// <summary>
    /// Whether a changed file is of a kind the extraction reads, and not build output.
    /// </summary>
    /// <param name="path">The file that changed.</param>
    /// <param name="root">The watched directory it was reported under.</param>
    public static bool IsReadKind(string path, string root) =>
        ReadExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
        && BuildOutputFilter.IsAnalyzable(path, root);

    /// <summary>
    /// The sibling project directories beside a module whose source the extraction reads.
    /// </summary>
    /// <remarks>
    /// Uses the same parent-directory pattern as ModelAnalyzer for xafml discovery. This is where Blazor.Server
    /// and Win controllers and editors are found, which is why it is shared rather than repeated.
    /// </remarks>
    public static List<string> SiblingDirectories(string moduleDirectory)
    {
        var siblings = new List<string>();

        var parentDir = Directory.GetParent(moduleDirectory)?.FullName;
        if (parentDir == null) return siblings;

        foreach (var siblingDir in Directory.GetDirectories(parentDir))
        {
            // Skip the module directory itself
            if (siblingDir.Equals(moduleDirectory, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip common non-project directories
            var dirName = Path.GetFileName(siblingDir);
            if (dirName.StartsWith(".") || dirName == "packages" || dirName == "node_modules")
                continue;

            // Only include siblings that have at least one .cs file (actual project dirs)
            try
            {
                if (Directory.GetFiles(siblingDir, "*.cs", SearchOption.AllDirectories)
                    .Any(f => BuildOutputFilter.IsAnalyzable(f, siblingDir)))
                {
                    siblings.Add(siblingDir);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip inaccessible directories
            }
        }

        return siblings;
    }

    /// <summary>
    /// The module, its siblings, and every project either of them references.
    /// </summary>
    /// <remarks>
    /// The entity analyzer runs once on the module and once on each sibling, and each run follows that
    /// directory's own project references. A Blazor.Server project's references are read too.
    /// </remarks>
    private static IEnumerable<string> CodeDirectories(string module)
    {
        var directories = new List<string> { module };

        try
        {
            directories.AddRange(SiblingDirectories(module));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Siblings that cannot be listed cannot be read either.
        }

        foreach (var directory in directories.ToList())
            directories.AddRange(ProjectFile.ReferencedDirectories(directory));

        return directories.Select(Path.GetFullPath).Distinct(PathComparer);
    }

    private static void Add(HashSet<string> files, string directory, string pattern, SearchOption option)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, pattern, option))
            {
                if (BuildOutputFilter.IsAnalyzable(file, directory))
                    files.Add(Path.GetFullPath(file));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A directory that cannot be listed cannot be read by the extraction either.
        }
    }

    private static IEnumerable<string> Subdirectories(string directory)
    {
        try
        {
            return Directory.GetDirectories(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool IsUnder(string path, string root) =>
        path.Equals(root, PathComparison)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, PathComparison);
}
