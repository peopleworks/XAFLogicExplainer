namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Decides whether a source file belongs to build output and should be skipped.
/// </summary>
/// <remarks>
/// Analyzers previously tested <c>file.Contains("bin")</c> and <c>file.Contains("obj")</c>. That
/// matches the substring anywhere in an absolute path, so a project living under
/// <c>C:\bin\Sales</c>, <c>D:\projects\robinson\</c> or any directory whose name merely contains
/// those three letters had every one of its files silently skipped — extraction would report an
/// application with no entities at all and no indication why.
/// <para>
/// Matching whole path segments is what was meant all along.
/// </para>
/// </remarks>
public static class BuildOutputFilter
{
    private static readonly string[] OutputDirectoryNames = ["bin", "obj"];

    /// <summary>
    /// Whether a path passes through a <c>bin</c> or <c>obj</c> directory.
    /// </summary>
    /// <param name="path">File or directory path, absolute or relative.</param>
    /// <param name="rootDirectory">
    /// The directory being analyzed. When given, only the part of the path below it is considered,
    /// so a project that happens to live under a directory named <c>bin</c> is analyzed normally.
    /// Build output is always below the project root; anything above it is somebody's folder name.
    /// </param>
    public static bool IsBuildOutput(string? path, string? rootDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var relative = Relativize(path, rootDirectory);
        var segments = relative.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            foreach (var name in OutputDirectoryNames)
            {
                if (segment.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a path is source worth analyzing.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="rootDirectory">The directory being analyzed, when known.</param>
    public static bool IsAnalyzable(string? path, string? rootDirectory = null) =>
        !IsBuildOutput(path, rootDirectory);

    /// <summary>
    /// Reduces a path to the part below the analyzed root, or leaves it alone when that cannot be
    /// established.
    /// </summary>
    private static string Relativize(string path, string? rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            return path;

        try
        {
            var relative = Path.GetRelativePath(rootDirectory, path);

            // A path outside the root comes back with a leading "..", which tells us nothing about
            // build output; fall back to examining the whole path in that case.
            return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
        }
        catch (ArgumentException)
        {
            // Different drive roots, or an unusable path. Examine what we were given.
            return path;
        }
    }
}
