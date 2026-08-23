using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// How a generated document points back at the source a claim came from.
/// </summary>
/// <remarks>
/// Every statement in these documents is supposed to be checkable, which means the reader has to be
/// able to open the thing being described. One implementation rather than one per generator,
/// because the two rules below are easy to get subtly different and a citation that differs by
/// generator is a citation a reader learns not to rely on.
/// </remarks>
public static class SourceCitation
{
    /// <summary>
    /// A path relative to the project, with its line, ready to print as inline code.
    /// </summary>
    /// <remarks>
    /// Forward slashes whatever the machine wrote them with. These documents get committed and read
    /// elsewhere, and a separator that changed with the extractor's operating system would put a
    /// diff in every citation the day somebody else regenerated the file.
    /// <para>
    /// A line of zero means the extraction never established one, and the file alone is cited
    /// rather than a line nobody can trust: <c>Foo.cs:0</c> reads like a location and is not one.
    /// </para>
    /// </remarks>
    /// <param name="project">The project the path is relative to.</param>
    /// <param name="filePath">Absolute path recorded by the extraction, or empty.</param>
    /// <param name="line">One-based line, or zero when unknown.</param>
    public static string Of(ExtractedProject project, string? filePath, int line)
    {
        if (string.IsNullOrEmpty(filePath))
            return "";

        var path = Relative(project, filePath).Replace('\\', '/');

        return line > 0 ? $"`{path}:{line}`" : $"`{path}`";
    }

    /// <summary>
    /// The shortest honest way to name a file: relative to the project, else to the solution
    /// beside it, else by name alone.
    /// </summary>
    /// <remarks>
    /// Not everything an extraction cites is inside the module. A shop that designs reports outside
    /// Visual Studio keeps the <c>.repx</c> exports in a folder beside it, and the first version of
    /// this printed those as <c>C:/Proyecto/.../Reporting/Summary.repx</c> — the absolute path of
    /// whichever machine ran the extraction, in a document meant to be committed. Every regeneration
    /// elsewhere would rewrite it.
    /// <para>
    /// Falling back to the file name is the last resort rather than an absolute path: a name a
    /// reader has to search for is worse than a path, and better than one that is wrong everywhere
    /// but here.
    /// </para>
    /// </remarks>
    private static string Relative(ExtractedProject project, string filePath)
    {
        if (project.ProjectPath.Length == 0)
            return filePath;

        if (Below(project.ProjectPath, filePath) is { } insideProject)
            return insideProject;

        // The directory holding the module usually holds the whole solution, which is where the
        // platform projects and the loose report exports live.
        var solutionRoot = Path.GetDirectoryName(project.ProjectPath.TrimEnd('/', '\\'));

        if (solutionRoot is { Length: > 0 } && Below(solutionRoot, filePath) is { } insideSolution)
            return $"../{insideSolution}";

        return Path.GetFileName(filePath);
    }

    /// <summary>The part of a path below a directory, or null when it is not below it.</summary>
    private static string? Below(string directory, string path)
    {
        if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase)
            || path.Length <= directory.Length)
        {
            return null;
        }

        // A separator has to sit at the boundary, so a project at `App.Module` does not swallow a
        // sibling directory named `App.Module2`.
        return path[directory.Length] is '/' or '\\'
            ? path[(directory.Length + 1)..]
            : null;
    }
}
