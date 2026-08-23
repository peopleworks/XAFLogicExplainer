using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Catalog;

/// <summary>
/// How far a framework claim about this application can be trusted.
/// </summary>
public enum CatalogTrustLevel
{
    /// <summary>No catalog was available, so the framework layer is simply unreported.</summary>
    None,

    /// <summary>
    /// A catalog informed the extraction, but the application declares no DevExpress version, so
    /// whether the two agree is unknown.
    /// </summary>
    Undeclared,

    /// <summary>The catalog describes the same release the application declares.</summary>
    Matched,

    /// <summary>
    /// The catalog describes a different release from the one the application declares — the
    /// answers are still mostly right, and no longer certainly right.
    /// </summary>
    Mismatched,
}

/// <summary>
/// Compares the catalog that informed an extraction against the framework version the application
/// actually declares.
/// </summary>
/// <remarks>
/// Before this existed, the newest catalog on the machine was used for every application and the
/// result never said so. On a machine holding one 26.1 catalog, a 23.2 application was told which
/// framework controllers load onto its screens using a catalog three releases ahead of it, in the
/// same confident sentence used when the two matched.
/// <para>
/// A mismatched catalog is still worth using — most of the framework is stable across releases, and
/// refusing to answer would throw away real information to avoid a small error. What is not
/// acceptable is answering at the same volume. Everything that reports a framework fact goes
/// through here so the qualification is written once and cannot be forgotten at one of the four
/// places that make such claims.
/// </para>
/// </remarks>
public static class CatalogTrust
{
    /// <summary>How far this project's framework claims can be trusted.</summary>
    /// <param name="project">An extracted project.</param>
    public static CatalogTrustLevel Of(ExtractedProject project)
    {
        var catalog = DeclaredDevExpressVersion.MajorMinor(project.CatalogVersion);

        if (catalog is null)
            return CatalogTrustLevel.None;

        var declared = DeclaredDevExpressVersion.MajorMinor(DeclaredDevExpressVersion.Of(project));

        if (declared is null)
            return CatalogTrustLevel.Undeclared;

        return string.Equals(catalog, declared, StringComparison.Ordinal)
            ? CatalogTrustLevel.Matched
            : CatalogTrustLevel.Mismatched;
    }

    /// <summary>
    /// The sentence to print wherever a framework fact is reported, or null when the catalog fits
    /// and nothing needs saying.
    /// </summary>
    /// <remarks>
    /// Null for <see cref="CatalogTrustLevel.None"/> as well: an absent catalog is already
    /// explained where the framework section would have been, and repeating it at every claim
    /// would be noise about something the reader has been told.
    /// </remarks>
    /// <param name="project">An extracted project.</param>
    /// <param name="spanish">Whether to write the sentence in Spanish.</param>
    public static string? Caveat(ExtractedProject project, bool spanish = false) =>
        Of(project) switch
        {
            CatalogTrustLevel.Mismatched when spanish =>
                $"Esta aplicacion declara DevExpress {DeclaredDevExpressVersion.Of(project)}, pero el "
                + $"catalogo disponible es de la {project.CatalogVersion}. Un controlador puede "
                + "aparecer, cambiar de destino o desaparecer entre versiones, asi que lo de abajo "
                + "es lo mas cercano que hay y no una certeza. Para responder con la version "
                + $"correcta: `xaflogic catalog build` en una maquina con DevExpress "
                + $"{DeclaredDevExpressVersion.Of(project)}.",

            CatalogTrustLevel.Mismatched =>
                $"This application declares DevExpress {DeclaredDevExpressVersion.Of(project)}, but "
                + $"the catalog available describes {project.CatalogVersion}. A controller can be "
                + "introduced, retargeted or removed between releases, so what follows is the "
                + "closest answer available rather than a certain one. To answer from the right "
                + $"release: `xaflogic catalog build` on a machine with DevExpress "
                + $"{DeclaredDevExpressVersion.Of(project)}.",

            CatalogTrustLevel.Undeclared when spanish =>
                $"Verificado contra el catalogo de DevExpress {project.CatalogVersion}. Este "
                + "proyecto no declara una version de DevExpress en su archivo de proyecto, asi "
                + "que no se pudo comprobar que sea la que corresponde.",

            CatalogTrustLevel.Undeclared =>
                $"Checked against the DevExpress {project.CatalogVersion} catalog. This project "
                + "declares no DevExpress version in its project file, so whether that is the "
                + "right release could not be confirmed.",

            _ => null,
        };
}
