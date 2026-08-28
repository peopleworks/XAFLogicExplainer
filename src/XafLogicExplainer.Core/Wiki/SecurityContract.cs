using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Wiki;

/// <summary>
/// The DevExpress security contracts a class can carry, and why carrying one matters to a corpus.
/// </summary>
/// <remarks>
/// The rule for what counts as <em>yours</em> is deliberately not a list of DevExpress class
/// names: a class is yours when its own source was read in one of the projects. That rule is
/// right, and it is exactly why the XAF Project Wizard scaffold slips through it. The wizard
/// writes <c>ApplicationUser</c> and <c>ApplicationUserLoginInfo</c> into
/// <c>SolutionName.Module\BusinessObjects\</c> of every solution created with v21.1 or later, so
/// their source <em>is</em> read -- and any two XAF applications built since 2021 look like they
/// share them. On a corpus of small applications that scaffold can be most of what the page says
/// was reused, and the first number a reader sees is the one it inflates.
/// </remarks>
internal static class SecurityContract
{
    /// <summary>
    /// The interfaces that make a class the security system user, or its login information.
    /// </summary>
    /// <remarks>
    /// This is a list of names, and there is no honest way to say otherwise. It is a different
    /// kind of list from the one the wiki refuses to keep: framework API surface that DevExpress
    /// documents and cannot rename without breaking every application implementing it, rather
    /// than the class names one template happened to emit this year. A developer who renames
    /// <c>ApplicationUser</c> to <c>Usuario</c> is still caught; a future template that picks new
    /// class names is still caught.
    /// </remarks>
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "ISecurityUser",
        "ISecurityUserWithLoginInfo",
        "ISecurityUserLoginInfo",
        "ISecurityUserLockout",
        "IOAuthSecurityUser",
        "IAuthenticationStandardUser",
        "IAuthenticationActiveDirectoryUser",
        "IPermissionPolicyUser",
    };

    /// <summary>
    /// Whether the class names one of those contracts in its base list.
    /// </summary>
    /// <remarks>
    /// Carrying a contract is never enough on its own to call something template code, which is
    /// why this only answers half the question. A developer who added <c>Department</c> and
    /// <c>Photo</c> to <c>ApplicationUser</c> in three applications really has built something,
    /// and the documented <c>Employee : Person, ISecurityUser, ...</c> shape is a business object
    /// that happens to log in. The shapes have to agree as well -- see
    /// <see cref="RecurringEntity.IsTemplate"/>, and the wording it earns on the page, which
    /// claims only what was read and never that a wizard typed it.
    /// </remarks>
    public static bool IsCarriedBy(ExtractedEntity entity) => CarriedBy(entity).Count > 0;

    /// <summary>
    /// The contracts the class names, in the order it names them.
    /// </summary>
    /// <remarks>
    /// Returned rather than merely counted so the page can print the evidence beside the claim.
    /// A card that says "framework" and nothing else asks to be trusted; one that says which
    /// contract the class carries can be checked against the file it cites.
    /// </remarks>
    public static IReadOnlyList<string> CarriedBy(ExtractedEntity entity) =>
        entity is null ? [] : [.. entity.BaseTypes.Where(Names.Contains)];
}
