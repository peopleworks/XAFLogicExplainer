namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// What a generator stamps on a page when nobody told it which version produced it.
/// </summary>
/// <remarks>
/// It used to be a version number, written once and then left behind: the explainer defaulted to
/// <c>0.10.1</c> six releases after 0.10.1 shipped, and nothing failed, because a default nobody
/// reaches is a default nobody checks. A page carrying a confidently wrong version is worse than
/// one admitting it does not know — the wrong number sends somebody looking for a bug in a release
/// that never generated the file.
/// <para>
/// The same argument this tool makes about the applications it reads, turned on the tool itself.
/// </para>
/// </remarks>
public static class GeneratorVersion
{
    /// <summary>
    /// Reads as a sentence where a version number would go, and can never be mistaken for one.
    /// </summary>
    public const string Unknown = "of unknown version";
}
