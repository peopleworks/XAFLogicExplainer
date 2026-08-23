namespace XafLogicExplainer.Core.Catalog;

/// <summary>
/// Where generated catalogs live on disk, and how they are read back.
/// </summary>
/// <remarks>
/// Deliberately outside any repository. A catalog is derived from a licensed DevExpress
/// installation, so it belongs to the machine that generated it and must not travel with source
/// control — see <c>NOTICE.md</c>.
/// </remarks>
public static class XafCatalogStore
{
    /// <summary>Directory holding generated catalogs.</summary>
    public static string DefaultDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".xaflogic",
            "catalog");

    /// <summary>
    /// Builds the file name for a DevExpress version.
    /// </summary>
    /// <param name="devExpressVersion">Version, e.g. "26.1".</param>
    public static string FileNameFor(string devExpressVersion) =>
        $"xaf-{Sanitize(devExpressVersion)}.dxcatalog.json";

    /// <summary>
    /// Loads the most recently generated catalog, or null when none exists.
    /// </summary>
    /// <remarks>
    /// Returning null is an ordinary outcome, not an error: the catalog is optional and most
    /// people will never generate one. Everything downstream must work without it.
    /// </remarks>
    /// <param name="directory">Directory to read, or null for the default.</param>
    public static XafCatalog? LoadLatest(string? directory = null)
    {
        var root = directory ?? DefaultDirectory;

        if (!Directory.Exists(root))
            return null;

        try
        {
            var newest = Directory
                .EnumerateFiles(root, "*.dxcatalog.json")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();

            return newest is null ? null : XafCatalog.FromJson(File.ReadAllText(newest.FullName));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unreadable catalog must not stop extraction; it only makes it less informed.
            return null;
        }
    }

    /// <summary>
    /// Loads the catalog describing a given DevExpress release, falling back to the most recently
    /// generated one when that release has no catalog on this machine.
    /// </summary>
    /// <remarks>
    /// The fallback is deliberate. Most of the framework is stable across releases, so a catalog
    /// one or two versions out still answers most questions correctly, and refusing to answer would
    /// discard real information to avoid a small error. The obligation it creates is to say so:
    /// callers compare <see cref="Models.ExtractedProject.CatalogVersion"/> against
    /// <see cref="Models.ExtractedProject.DeclaredDevExpressVersion"/> through
    /// <see cref="CatalogTrust"/> and qualify what they report.
    /// <para>
    /// Before this existed the newest catalog was used unconditionally, which on a machine holding
    /// a single 26.1 catalog meant a 17.1 application was described using a framework nine years
    /// ahead of it, silently.
    /// </para>
    /// </remarks>
    /// <param name="devExpressVersion">
    /// The version the application declares, e.g. <c>23.2</c>. Null falls straight back to the
    /// newest, which is what an application that declares nothing can be given.
    /// </param>
    /// <param name="directory">Directory to read, or null for the default.</param>
    public static XafCatalog? LoadFor(string? devExpressVersion, string? directory = null)
    {
        var wanted = DeclaredDevExpressVersion.MajorMinor(devExpressVersion);

        if (wanted is null)
            return LoadLatest(directory);

        foreach (var path in List(directory))
        {
            if (!string.Equals(wanted, VersionOfFile(path), StringComparison.Ordinal))
                continue;

            try
            {
                if (XafCatalog.FromJson(File.ReadAllText(path)) is { } matched)
                    return matched;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable is the same as absent here: fall through to the newest.
            }
        }

        return LoadLatest(directory);
    }

    /// <summary>
    /// The <c>major.minor</c> a catalog file name encodes, or null when the name is not one of ours.
    /// </summary>
    private static string? VersionOfFile(string path)
    {
        var name = Path.GetFileName(path);

        const string prefix = "xaf-";
        const string suffix = ".dxcatalog.json";

        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return DeclaredDevExpressVersion.MajorMinor(
            name[prefix.Length..^suffix.Length]);
    }

    /// <summary>
    /// Writes a catalog and returns the path it was written to.
    /// </summary>
    /// <param name="catalog">The catalog to save.</param>
    /// <param name="directory">Directory to write to, or null for the default.</param>
    public static string Save(XafCatalog catalog, string? directory = null)
    {
        var root = directory ?? DefaultDirectory;
        Directory.CreateDirectory(root);

        var path = Path.Combine(root, FileNameFor(catalog.DevExpressVersion));
        File.WriteAllText(path, catalog.ToJson());

        return path;
    }

    /// <summary>Lists the catalogs available, newest first.</summary>
    /// <param name="directory">Directory to read, or null for the default.</param>
    public static IReadOnlyList<string> List(string? directory = null)
    {
        var root = directory ?? DefaultDirectory;

        if (!Directory.Exists(root))
            return [];

        try
        {
            return Directory
                .EnumerateFiles(root, "*.dxcatalog.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "unknown";

        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '-' : c).ToArray());
    }
}
