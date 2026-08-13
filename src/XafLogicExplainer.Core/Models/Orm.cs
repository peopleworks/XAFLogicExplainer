namespace XafLogicExplainer.Core.Models;

/// <summary>
/// How an application's ORM is named, wherever it is reported.
/// </summary>
/// <remarks>
/// One definition, because there were three. When <see cref="OrmType.Unknown"/> arrived, the agent
/// files learned it and the HTML explainer and the MCP overview did not — each of those decided the
/// question again, in a binary that had no third answer, so a project whose ORM the tool could not
/// determine was reported as XPO. The defect was not the wrong answer; it was that three places
/// were each entitled to one.
/// <para>
/// The ORM travels as a string rather than the enum because <see cref="ExtractedProject.OrmType"/>
/// is what a rendered snapshot carries and what the MCP server reads back. Comparisons are made
/// here so that stays an implementation detail nobody downstream copies.
/// </para>
/// </remarks>
public static class Orm
{
    /// <summary>The name for a heading or a table cell, where it stands on its own.</summary>
    public static string Label(string? ormType) =>
        IsUnknown(ormType) ? "Not determined"
        : IsEfCore(ormType) ? "Entity Framework Core"
        : "XPO";

    /// <summary>
    /// The name for the middle of a sentence, where it needs an article.
    /// </summary>
    /// <remarks>
    /// A second form rather than a second decision: both are written here, so neither can drift
    /// into disagreeing with the other about what is known. Only the grammar differs.
    /// </remarks>
    public static string DisplayName(string? ormType) =>
        IsUnknown(ormType) ? "an undetermined ORM"
        : IsEfCore(ormType) ? "Entity Framework Core"
        : "XPO";

    /// <summary>Whether the source said nothing either way.</summary>
    /// <remarks>
    /// A null is unknown too. Anything that never ran detection has not learned XPO.
    /// </remarks>
    public static bool IsUnknown(string? ormType) =>
        string.IsNullOrWhiteSpace(ormType)
        || ormType.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the application persists through Entity Framework Core.</summary>
    public static bool IsEfCore(string? ormType) =>
        ormType is not null
        && !IsUnknown(ormType)
        && ormType.Contains("EF", StringComparison.OrdinalIgnoreCase);
}
