using System.Text.RegularExpressions;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Which framework an application says it targets, however its project file spells it.
/// </summary>
/// <remarks>
/// Read from the project file as text, like everything else here, so it needs no build and no
/// installed SDK.
/// <para>
/// <strong>Three spellings, because XAF applications outlive project formats.</strong> An
/// SDK-style project writes <c>&lt;TargetFramework&gt;</c>, or <c>&lt;TargetFrameworks&gt;</c> when
/// it multi-targets. A project from before the SDK format writes
/// <c>&lt;TargetFrameworkVersion&gt;v4.8&lt;/TargetFrameworkVersion&gt;</c> and has no
/// <c>&lt;TargetFramework&gt;</c> at all. Reading only the first returns nothing for exactly the
/// applications where the constraint is tightest, because a .NET Framework project is the one
/// place most modern C# does not compile.
/// </para>
/// <para>
/// Silence is not a neutral outcome here. An agent handed a document that says nothing about the
/// framework assumes a modern one and reaches for nullable reference types, <c>record</c>,
/// file-scoped namespaces and collection expressions, none of which build on <c>net48</c>. That is
/// the same failure as reporting no reports for an application that has forty: an absence read as
/// information.
/// </para>
/// </remarks>
public static class DeclaredTargetFramework
{
    /// <summary><c>&lt;TargetFramework&gt;net9.0&lt;/TargetFramework&gt;</c>.</summary>
    private static readonly Regex SdkForm = new(
        @"<TargetFramework>\s*(?<tfm>[^<]+?)\s*</TargetFramework>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <c>&lt;TargetFrameworks&gt;net8.0;net9.0&lt;/TargetFrameworks&gt;</c> — a project that
    /// multi-targets.
    /// </summary>
    private static readonly Regex MultiForm = new(
        @"<TargetFrameworks>\s*(?<tfms>[^<]+?)\s*</TargetFrameworks>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <c>&lt;TargetFrameworkVersion&gt;v4.8&lt;/TargetFrameworkVersion&gt;</c> — the pre-SDK
    /// spelling, and the only one a .NET Framework project has.
    /// </summary>
    private static readonly Regex LegacyForm = new(
        @"<TargetFrameworkVersion>\s*v?(?<version>\d+(?:\.\d+)*)\s*</TargetFrameworkVersion>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The framework a project file declares, or null when it declares none in any spelling.
    /// </summary>
    /// <remarks>
    /// Null is an ordinary outcome and stays distinct from a value: a module whose framework comes
    /// from a shared <c>Directory.Build.props</c> declares nothing here, and a project file that
    /// could not be found at all declares nothing either. "I did not read one" and "it targets
    /// net48" must never render the same way, which is the whole point of the fix.
    /// <para>
    /// A multi-targeting project is reported as it declared itself, semicolons and all. Picking
    /// one of the list would be inventing a fact, and the list is what an agent needs: code has to
    /// compile on every framework named there, so the oldest one is the real constraint.
    /// </para>
    /// </remarks>
    public static string? FromProjectFile(string? projectFileContent)
    {
        if (string.IsNullOrWhiteSpace(projectFileContent))
            return null;

        if (SdkForm.Match(projectFileContent) is { Success: true } sdk)
            return sdk.Groups["tfm"].Value;

        if (MultiForm.Match(projectFileContent) is { Success: true } multi)
            return multi.Groups["tfms"].Value;

        if (LegacyForm.Match(projectFileContent) is { Success: true } legacy)
            return Moniker(legacy.Groups["version"].Value);

        return null;
    }

    /// <summary>
    /// Whether a moniker names .NET Framework, where most modern C# does not compile.
    /// </summary>
    /// <remarks>
    /// Matched on the moniker rather than remembered from the parse, so it is equally right about
    /// an SDK-style project that targets <c>net472</c> — which is legal, and is how a migrated
    /// application often looks halfway through. <c>net5.0</c> and everything after carry a dot;
    /// .NET Framework monikers never do.
    /// </remarks>
    public static bool IsDotNetFramework(string? moniker) =>
        !string.IsNullOrWhiteSpace(moniker)
        && DotNetFrameworkMoniker.IsMatch(moniker);

    private static readonly Regex DotNetFrameworkMoniker = new(
        @"^net[1-4]\d*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <c>4.8</c> becomes <c>net48</c>, <c>4.8.1</c> becomes <c>net481</c>.
    /// </summary>
    /// <remarks>
    /// Dots removed, which is the whole of the .NET Framework moniker rule and is why
    /// <c>net481</c> and <c>net48</c> are different frameworks rather than a typo of each other.
    /// </remarks>
    private static string Moniker(string version) =>
        "net" + version.Replace(".", string.Empty, StringComparison.Ordinal);
}
