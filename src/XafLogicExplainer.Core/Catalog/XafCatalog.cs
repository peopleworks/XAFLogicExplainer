using System.Text.Json;
using System.Text.Json.Serialization;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Catalog;

/// <summary>
/// What the XAF framework itself provides: its attributes, controllers, model interfaces and modules.
/// </summary>
/// <remarks>
/// Extraction reads an application's source without knowing anything about the framework it is
/// written against. That makes one question unanswerable: is <c>DeleteObjectsViewController</c>
/// something this team wrote, or something DevExpress ships? Without an answer, generated
/// documentation presents framework behavior and the application's own logic as though they were
/// the same thing — and an agent asked to change the former will happily try.
/// <para>
/// The catalog supplies that ground truth. It is generated locally by a licensee from their own
/// DevExpress installation (see <c>xaflogic catalog build</c>) and written outside the repository.
/// <strong>Nothing derived from DevExpress is distributed with this project.</strong>
/// </para>
/// <para>
/// It is entirely optional. With no catalog present, extraction behaves exactly as it did before
/// one existed.
/// </para>
/// </remarks>
public sealed class XafCatalog
{
    /// <summary>Version of the DevExpress installation this was generated from, e.g. "26.1".</summary>
    public string DevExpressVersion { get; init; } = string.Empty;

    /// <summary>When it was generated, in round-trip format.</summary>
    public string GeneratedAt { get; init; } = string.Empty;

    /// <summary>Assemblies that were read.</summary>
    public List<string> Assemblies { get; init; } = [];

    /// <summary>XAF attributes, keyed by simple type name.</summary>
    public Dictionary<string, XafCatalogType> Attributes { get; init; } = [];

    /// <summary>Framework controllers, keyed by simple type name.</summary>
    public Dictionary<string, XafCatalogType> Controllers { get; init; } = [];

    /// <summary>Application Model interfaces, keyed by simple type name.</summary>
    public Dictionary<string, XafCatalogType> ModelInterfaces { get; init; } = [];

    /// <summary>Framework modules, keyed by simple type name.</summary>
    public Dictionary<string, XafCatalogType> Modules { get; init; } = [];

    /// <summary>Total number of framework types recorded.</summary>
    [JsonIgnore]
    public int TypeCount =>
        Attributes.Count + Controllers.Count + ModelInterfaces.Count + Modules.Count;

    /// <summary>
    /// Looks up an attribute by the name as written in source, with or without the suffix.
    /// </summary>
    /// <remarks>
    /// C# lets <c>[Description]</c> stand for <c>DescriptionAttribute</c>, and source is written
    /// both ways, so both must resolve.
    /// </remarks>
    /// <param name="attributeName">Name as it appears in the attribute list.</param>
    public XafCatalogType? FindAttribute(string? attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
            return null;

        var name = attributeName.Trim();

        if (Attributes.TryGetValue(name, out var exact))
            return exact;

        return Attributes.TryGetValue(name + "Attribute", out var suffixed) ? suffixed : null;
    }

    /// <summary>
    /// Looks up a controller by name, ignoring any generic arguments.
    /// </summary>
    /// <param name="typeName">A base type as written in source, e.g. <c>ViewController&lt;DetailView&gt;</c>.</param>
    public XafCatalogType? FindController(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        var name = typeName.Trim();

        var generic = name.IndexOf('<');
        if (generic > 0)
            name = name[..generic];

        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name[(lastDot + 1)..];

        return Controllers.TryGetValue(name, out var controller) ? controller : null;
    }

    /// <summary>Serialization settings shared by the generator and the loader.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Serializes the catalog.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Deserializes a catalog, returning null when the text is not one.</summary>
    /// <param name="json">Catalog JSON.</param>
    public static XafCatalog? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<XafCatalog>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// One type the XAF framework provides.
/// </summary>
public sealed class XafCatalogType
{
    /// <summary>Simple type name, e.g. <c>DeleteObjectsViewController</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Namespace it lives in.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Assembly it ships in, without the version suffix.</summary>
    public string Assembly { get; init; } = string.Empty;

    /// <summary>Immediate base type, for controllers.</summary>
    public string? BaseType { get; init; }

    /// <summary>First sentence of the official documentation, when the XML docs supplied one.</summary>
    public string? Summary { get; init; }

    /// <summary>Official documentation URL, when the XML docs linked one.</summary>
    public string? DocumentationUrl { get; init; }

    /// <summary>Whether the type is abstract, which for a controller means it is meant to be derived from.</summary>
    public bool IsAbstract { get; init; }

    /// <summary>
    /// Whether the type is marked <c>[Obsolete]</c>.
    /// </summary>
    /// <remarks>
    /// XAF filters obsolete controllers out when it collects them from an assembly, so one listed
    /// as running on a screen would never actually be there.
    /// </remarks>
    public bool IsObsolete { get; init; }

    /// <summary>
    /// Where XAF activates this controller, when it could be determined. Null means unknown.
    /// </summary>
    /// <remarks>
    /// Settable, unlike the rest, because a catalog is built in two passes: reflection over the
    /// assemblies first, then the installed sources if they are present.
    /// </remarks>
    public ControllerTargeting? Targeting { get; set; }

    /// <summary>
    /// How <see cref="Targeting"/> was obtained.
    /// </summary>
    /// <remarks>
    /// <c>sources</c> is complete: the constructor was read, so an unset condition really is
    /// unrestricted. <c>reflection</c> is a <em>lower bound</em> — it sees only what the generic
    /// base type states, and four out of five framework controllers assign their targeting inside a
    /// constructor, which assembly metadata does not carry. Anything deciding whether a controller
    /// runs on a view has to treat the two differently or it will claim certainty it does not have.
    /// </remarks>
    public string? TargetingSource { get; set; }

    /// <summary>Full name, for display.</summary>
    [JsonIgnore]
    public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
}
