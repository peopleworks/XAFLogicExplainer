namespace XafLogicExplainer.Mcp;

/// <summary>
/// Finds the XAF module to answer questions about when nobody said where it is.
/// </summary>
/// <remarks>
/// Both entry points need this and neither can ask. A marketplace plugin declares
/// <c>xaflogic mcp</c> with no arguments because it cannot know anyone's paths, and an MCP client
/// launching <c>dnx XafLogicExplainer.Mcp</c> is in the same position. Discovery is what makes
/// "install it and it works" true rather than aspirational.
/// </remarks>
public static class XafModuleLocator
{
    /// <summary>How far below the starting directory to look.</summary>
    /// <remarks>
    /// Two levels covers the conventional layout — a module inside a solution folder — without
    /// turning a launch in a large tree into a full recursive scan.
    /// </remarks>
    private const int MaxDepth = 2;

    /// <summary>
    /// Looks for an XAF module at or below a directory.
    /// </summary>
    /// <param name="startDirectory">Where to start, usually the working directory.</param>
    /// <returns>
    /// The module directory, or null when nothing convincing was found. Null on purpose: guessing
    /// wrong is worse than asking, because the server would then answer confidently about the
    /// wrong application.
    /// </returns>
    public static string? Locate(string startDirectory)
    {
        if (!Directory.Exists(startDirectory))
            return null;

        if (LooksLikeModule(startDirectory))
            return startDirectory;

        return Search(startDirectory, MaxDepth);
    }

    private static string? Search(string directory, int depthRemaining)
    {
        if (depthRemaining == 0)
            return null;

        try
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                if (LooksLikeModule(child))
                    return child;

                var deeper = Search(child, depthRemaining - 1);
                if (deeper is not null)
                    return deeper;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable directory is not a reason to abandon the search.
        }

        return null;
    }

    /// <summary>
    /// Whether a directory holds an XAF module: a project file, and a class deriving from
    /// <c>ModuleBase</c>.
    /// </summary>
    private static bool LooksLikeModule(string directory)
    {
        try
        {
            if (!Directory.EnumerateFiles(directory, "*.csproj").Any())
                return false;

            return Directory
                .EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly)
                .Any(file => File.ReadAllText(file).Contains("ModuleBase", StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
