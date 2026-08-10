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
