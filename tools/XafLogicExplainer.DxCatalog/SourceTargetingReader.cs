using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Catalog;

namespace XafLogicExplainer.DxCatalog;

/// <summary>
/// Fills in framework controller targeting from the installed DevExpress sources.
/// </summary>
/// <remarks>
/// The reflection pass can only read targeting a controller states in its generic base type, and
/// most framework controllers do not: they assign <c>TargetViewType</c>, <c>TargetObjectType</c>,
/// <c>TargetViewNesting</c> or <c>TargetViewId</c> inside a constructor, which assembly metadata
/// does not carry. Those constructors are ordinary C#, and this project reads ordinary C# for a
/// living, so the same <see cref="ControllerTargetingReader"/> that reads an application's
/// controllers reads DevExpress's.
/// <para>
/// Source code is an optional component of the DevExpress installer. When it is absent the catalog
/// keeps whatever reflection found and says so, rather than presenting a partial answer as a
/// complete one.
/// </para>
/// </remarks>
public sealed class SourceTargetingReader
{
    /// <summary>
    /// Source trees worth walking.
    /// </summary>
    /// <remarks>
    /// Every XAF controller in the 26.1 sources lives under one of these. The rest of the tree is
    /// enormous and holds none: <c>Win</c> alone is 35,000 files of WinForms components, and
    /// walking it would multiply the time for nothing.
    /// </remarks>
    private static readonly string[] TreePrefixes =
    [
        "DevExpress.ExpressApp",
        "DevExpress.Persistent",
    ];

    private readonly Action<string> _report;

    /// <summary>Creates a reader.</summary>
    /// <param name="report">Progress callback.</param>
    public SourceTargetingReader(Action<string>? report = null) => _report = report ?? (_ => { });

    /// <summary>
    /// Reads the sources and upgrades every controller they explain.
    /// </summary>
    /// <param name="catalog">The catalog to complete, modified in place.</param>
    /// <param name="sourceDirectory">The <c>Components/Sources</c> directory.</param>
    /// <returns>How many controllers gained targeting read from source.</returns>
    public int Apply(XafCatalog catalog, string sourceDirectory)
    {
        var trees = Directory
            .EnumerateDirectories(sourceDirectory)
            .Where(directory => TreePrefixes.Any(prefix =>
                Path.GetFileName(directory).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (trees.Count == 0)
        {
            _report($"  no XAF source trees under {sourceDirectory}");
            return 0;
        }

        // Reflection names an open generic ConfirmationViewControllerBase`1; its declaration is
        // called ConfirmationViewControllerBase. Matching on the bare name lets a generic base
        // class contribute the targeting every closed subclass inherits, which is otherwise lost
        // for the whole family.
        var byBareName = catalog.Controllers.Values
            .GroupBy(entry => entry.Name.Split('`')[0], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var read = 0;
        var upgraded = 0;

        foreach (var tree in trees)
        {
            var before = upgraded;

            foreach (var file in Directory.EnumerateFiles(tree, "*.cs", SearchOption.AllDirectories))
            {
                string text;

                try
                {
                    text = File.ReadAllText(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                // Parsing is the expensive part, and most files declare no controller at all.
                if (!text.Contains("Controller", StringComparison.Ordinal))
                    continue;

                read++;
                upgraded += Apply(byBareName, text, file);
            }

            _report($"  {Path.GetFileName(tree)}: {upgraded - before} controllers");
        }

        _report($"  {read} source files read");

        return upgraded;
    }

    /// <summary>
    /// Applies one file's controller declarations to the catalog.
    /// </summary>
    private static int Apply(
        IReadOnlyDictionary<string, List<XafCatalogType>> byBareName,
        string text,
        string path)
    {
        var root = CSharpSyntaxTree.ParseText(text, path: path).GetRoot();
        var upgraded = 0;

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            // Only types the reflection pass already recorded. That keeps the two passes describing
            // the same set: a class in the sources that is internal, or that ships in an assembly
            // outside the catalog's scope, is not part of the framework surface an application can
            // see.
            if (!byBareName.TryGetValue(classDecl.Identifier.Text, out var entries))
                continue;

            foreach (var entry in entries)
            {
                // A partial class split across files would otherwise let the half without the
                // constructor overwrite the half with it.
                if (entry.TargetingSource == "sources" && classDecl.Modifiers.Any(SyntaxKind.PartialKeyword))
                    continue;

                // Read per entry rather than once: the arity variants of a name are separate
                // catalog types, and inheritance resolution mutates what it is given.
                entry.Targeting = ControllerTargetingReader.Read(classDecl);
                entry.TargetingSource = "sources";
                upgraded++;
            }
        }

        return upgraded;
    }
}
