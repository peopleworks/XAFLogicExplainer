using System.Text;
using System.Xml.Linq;

namespace XafLogicExplainer.DxCatalog;

/// <summary>
/// Reads the summaries DevExpress ships alongside its assemblies.
/// </summary>
/// <remarks>
/// Metadata alone gives names and inheritance; the XML documentation gives meaning. It also
/// embeds links to docs.devexpress.com, so a catalog entry can point at the official page for the
/// concept rather than merely asserting that a type exists.
/// </remarks>
public sealed class XmlDocumentationReader
{
    private readonly Dictionary<string, DocEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>How many documented types were loaded.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Loads the XML file that accompanies an assembly, if there is one.
    /// </summary>
    /// <param name="assemblyPath">Path to the .dll; the .xml beside it is read.</param>
    public void LoadFor(string assemblyPath)
    {
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

        if (!File.Exists(xmlPath))
            return;

        try
        {
            var document = XDocument.Load(xmlPath);

            foreach (var member in document.Descendants("member"))
            {
                var name = member.Attribute("name")?.Value;

                // "T:" members are types. Methods, properties and fields are not catalogued.
                if (name is null || !name.StartsWith("T:", StringComparison.Ordinal))
                    continue;

                var summaryElement = member.Element("summary");
                if (summaryElement is null)
                    continue;

                _entries[name[2..]] = new DocEntry(
                    FirstSentence(Flatten(summaryElement)),
                    FirstDocumentationLink(summaryElement));
            }
        }
        catch (Exception ex) when (ex is IOException or System.Xml.XmlException)
        {
            // A malformed or locked XML file costs summaries, not the catalog.
        }
    }

    /// <summary>Returns the documentation for a type, if it was loaded.</summary>
    /// <param name="fullTypeName">Namespace-qualified type name.</param>
    public DocEntry? Find(string fullTypeName) =>
        _entries.TryGetValue(fullTypeName, out var entry) ? entry : null;

    /// <summary>
    /// Turns mixed XML content into plain prose.
    /// </summary>
    /// <remarks>
    /// Summaries are wrapped in <c>&lt;para&gt;</c> and riddled with <c>&lt;see cref="T:..."&gt;</c>
    /// references. Kept as-is they read as noise; the useful part of a cref is the type name at its
    /// end, so that is what replaces it.
    /// </remarks>
    private static string Flatten(XElement element)
    {
        var text = new StringBuilder();
        Walk(element, text);
        return Collapse(text.ToString());
    }

    /// <summary>
    /// Appends an element's prose, treating <c>&lt;see&gt;</c> as a leaf.
    /// </summary>
    /// <remarks>
    /// A flat pass over <c>DescendantNodes</c> emits a reference twice — once as the resolved
    /// name and again as the element's own text node, turning "the Delete Action" into "the
    /// Delete ActionAction". Recursion is what lets a reference be replaced rather than added to.
    /// </remarks>
    private static void Walk(XElement element, StringBuilder text)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText textNode:
                    text.Append(textNode.Value);
                    break;

                case XElement { Name.LocalName: "see" } see:
                    text.Append(ReferenceText(see));
                    break;

                case XElement child:
                    Walk(child, text);
                    break;
            }
        }
    }

    /// <summary>
    /// Renders a documentation reference as the thing it names.
    /// </summary>
    private static string ReferenceText(XElement see)
    {
        var reference = see.Attribute("cref")?.Value;

        if (string.IsNullOrWhiteSpace(reference))
            return see.Value;

        // "T:DevExpress.ExpressApp.ViewController" -> "ViewController"
        var withoutPrefix = reference.Length > 2 && reference[1] == ':'
            ? reference[2..]
            : reference;

        var lastDot = withoutPrefix.LastIndexOf('.');
        return lastDot >= 0 ? withoutPrefix[(lastDot + 1)..] : withoutPrefix;
    }

    /// <summary>
    /// Finds the first docs.devexpress.com link in a summary.
    /// </summary>
    private static string? FirstDocumentationLink(XElement element) =>
        element.Descendants()
            .Where(e => e.Name.LocalName == "see")
            .Select(e => e.Attribute("href")?.Value)
            .FirstOrDefault(href =>
                !string.IsNullOrWhiteSpace(href) &&
                href.Contains("docs.devexpress.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Keeps the first sentence.
    /// </summary>
    /// <remarks>
    /// The catalog is read by agents with finite context and rendered into tables. A full
    /// DevExpress summary can run several paragraphs; the opening sentence carries what the type
    /// is, which is the whole question being asked of it.
    /// </remarks>
    private static string FirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var end = text.IndexOf(". ", StringComparison.Ordinal);

        if (end < 0)
            return text.EndsWith('.') ? text : text + ".";

        return text[..(end + 1)];
    }

    private static string Collapse(string text)
    {
        var collapsed = text.Replace('\r', ' ').Replace('\n', ' ').Trim();

        while (collapsed.Contains("  ", StringComparison.Ordinal))
            collapsed = collapsed.Replace("  ", " ", StringComparison.Ordinal);

        return collapsed;
    }
}

/// <summary>
/// Documentation for one type.
/// </summary>
/// <param name="Summary">First sentence of the official description.</param>
/// <param name="DocumentationUrl">Official documentation page, when one was linked.</param>
public sealed record DocEntry(string Summary, string? DocumentationUrl);
