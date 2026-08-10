using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Finds the property and list editors a team wrote themselves.
/// </summary>
/// <remarks>
/// A property with a custom editor does not render the way its type implies, and the business
/// class says nothing about it. The editors also usually live in the platform project beside the
/// module, so nobody reading the business objects ever meets them.
/// </remarks>
public class EditorAnalyzer
{
    /// <summary>Attributes that register an editor with XAF.</summary>
    private static readonly Dictionary<string, EditorKind> RegistrationAttributes = new(StringComparer.Ordinal)
    {
        ["PropertyEditor"] = EditorKind.PropertyEditor,
        ["ListEditor"] = EditorKind.ListEditor,
        ["ViewItem"] = EditorKind.ViewItem,
    };

    /// <summary>
    /// Base classes that mean "this is an editor" even without a registration attribute.
    /// </summary>
    /// <remarks>
    /// Matched as suffixes, which covers every platform in one line each:
    /// <c>BlazorPropertyEditorBase</c>, <c>WinPropertyEditor</c>, <c>ASPxPropertyEditor</c> all end
    /// the same way. An abstract editor shared by several concrete ones carries no attribute at
    /// all, and is still worth reporting.
    /// </remarks>
    private static readonly (string Suffix, EditorKind Kind)[] EditorBaseSuffixes =
    [
        ("PropertyEditorBase", EditorKind.PropertyEditor),
        ("PropertyEditor", EditorKind.PropertyEditor),
        ("ListEditorBase", EditorKind.ListEditor),
        ("ListEditor", EditorKind.ListEditor),
        ("ViewItemBase", EditorKind.ViewItem),
        ("ViewItem", EditorKind.ViewItem),
    ];

    /// <summary>
    /// Scans a project directory for editors.
    /// </summary>
    /// <param name="sourceDirectory">Project root.</param>
    /// <param name="options">Extraction options.</param>
    /// <param name="sharedConstants">
    /// Constants gathered from the whole solution. Required in practice: the editor lives in the
    /// platform project and the alias constant it names is declared in the module beside it, so a
    /// project read on its own resolves nothing.
    /// </param>
    public List<ExtractedEditor> AnalyzeEditors(
        string sourceDirectory,
        ExtractionOptions options,
        IReadOnlyDictionary<string, string>? sharedConstants = null)
    {
        var editors = new List<ExtractedEditor>();

        if (!Directory.Exists(sourceDirectory))
            return editors;

        // Local declarations win: a project that defines its own constant of the same name means
        // that one.
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);

        if (sharedConstants is not null)
        {
            foreach (var (key, value) in sharedConstants)
                constants[key] = value;
        }

        foreach (var (key, value) in CollectStringConstants(sourceDirectory))
            constants[key] = value;

        foreach (var file in EnumerateSource(sourceDirectory))
        {
            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            // Cheap gate: an editor names one of these somewhere, and most files name none.
            if (!source.Contains("Editor", StringComparison.Ordinal) &&
                !source.Contains("ViewItem", StringComparison.Ordinal))
            {
                continue;
            }

            var root = CSharpSyntaxTree.ParseText(source, path: file).GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var editor = Describe(classDecl, file, constants);
                if (editor is not null)
                    editors.Add(editor);
            }
        }

        return editors
            .GroupBy(e => e.ClassName, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(e => e.ClassName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Decides whether a class is an editor, and reads what it registers.
    /// </summary>
    private static ExtractedEditor? Describe(
        ClassDeclarationSyntax classDecl,
        string filePath,
        IReadOnlyDictionary<string, string> constants)
    {
        var registration = classDecl.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => (Attribute: attr, Name: SimpleAttributeName(attr.Name.ToString())))
            .FirstOrDefault(x => RegistrationAttributes.ContainsKey(x.Name));

        var baseType = classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? string.Empty;
        var baseKind = KindFromBaseType(baseType);

        // Either the attribute or the base type is enough. Requiring both would miss the abstract
        // base a team writes once and the concrete editor that only carries the attribute.
        if (registration.Attribute is null && baseKind == EditorKind.Unknown)
            return null;

        var editor = new ExtractedEditor
        {
            ClassName = classDecl.Identifier.Text,
            Namespace = NamespaceOf(classDecl),
            FilePath = filePath,
            BaseType = baseType,
            Kind = registration.Attribute is not null
                ? RegistrationAttributes[registration.Name]
                : baseKind,
            Description = SummaryOf(classDecl),
        };

        if (registration.Attribute?.ArgumentList is { } arguments)
            ReadRegistration(editor, arguments, constants);

        editor.ClientAssets.AddRange(FindClientAssets(classDecl.ToFullString()));

        return editor;
    }

    /// <summary>
    /// Reads <c>[PropertyEditor(typeof(T), alias, isDefault)]</c> and its shorter forms.
    /// </summary>
    /// <remarks>
    /// Read by argument shape rather than by position: <c>ListEditor</c> takes no alias, and
    /// <c>PropertyEditor</c> is written both with and without one, so counting positions gets the
    /// alias and the default flag confused in one of the three cases.
    /// </remarks>
    private static void ReadRegistration(
        ExtractedEditor editor,
        AttributeArgumentListSyntax arguments,
        IReadOnlyDictionary<string, string> constants)
    {
        foreach (var argument in arguments.Arguments)
        {
            switch (argument.Expression)
            {
                case TypeOfExpressionSyntax typeOf:
                    editor.TargetType = typeOf.Type.ToString();
                    break;

                case LiteralExpressionSyntax literal
                    when literal.IsKind(SyntaxKind.TrueLiteralExpression) ||
                         literal.IsKind(SyntaxKind.FalseLiteralExpression):
                    editor.IsDefault = literal.IsKind(SyntaxKind.TrueLiteralExpression);
                    break;

                default:
                    // A string, or a named constant standing in for one.
                    var value = SyntaxLiteral.ValueOf(argument.Expression);
                    editor.Alias = Resolve(value, constants);
                    break;
            }
        }
    }

    /// <summary>
    /// Turns a constant reference into the string it stands for.
    /// </summary>
    /// <remarks>
    /// Teams gather aliases in a constants struct, so the attribute reads
    /// <c>CustomEditorAliases.MapsMarkerPropertyEditor</c>. Reporting that verbatim leaks an
    /// implementation detail where the reader needs the value XAF actually matches on.
    /// </remarks>
    private static string Resolve(string? value, IReadOnlyDictionary<string, string> constants)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (constants.TryGetValue(value, out var byFullName))
            return byFullName;

        var lastDot = value.LastIndexOf('.');
        if (lastDot >= 0 && constants.TryGetValue(value[(lastDot + 1)..], out var byName))
            return byName;

        return value;
    }

    /// <summary>
    /// Gathers every <c>const string</c> in a project, by name and by <c>Type.Name</c>.
    /// </summary>
    /// <param name="sourceDirectory">Project root to read.</param>
    public static Dictionary<string, string> CollectStringConstants(string sourceDirectory)
    {
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in EnumerateSource(sourceDirectory))
        {
            string source;
            try
            {
                source = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            if (!source.Contains("const string", StringComparison.Ordinal))
                continue;

            var root = CSharpSyntaxTree.ParseText(source, path: file).GetRoot();

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (!field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
                        continue;

                    foreach (var variable in field.Declaration.Variables)
                    {
                        if (variable.Initializer?.Value is not LiteralExpressionSyntax literal)
                            continue;

                        if (!literal.IsKind(SyntaxKind.StringLiteralExpression))
                            continue;

                        var name = variable.Identifier.Text;
                        constants[name] = literal.Token.ValueText;
                        constants[$"{type.Identifier.Text}.{name}"] = literal.Token.ValueText;
                    }
                }
            }
        }

        return constants;
    }

    /// <summary>
    /// Finds client-side files an editor names.
    /// </summary>
    /// <remarks>
    /// A JavaScript file an editor loads is behaviour in neither C# nor XML, and it is why a
    /// control stops working after somebody renames an asset.
    /// </remarks>
    private static IEnumerable<string> FindClientAssets(string source)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in new[] { ".js", ".css", ".mjs" })
        {
            var index = 0;

            while ((index = source.IndexOf(extension, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var end = index + extension.Length;

                // Walk back to the opening quote; anything else is a false positive.
                var start = index;
                while (start > 0 && source[start - 1] is not ('"' or '\'' or '\n'))
                    start--;

                if (start > 0 && source[start - 1] is '"' or '\'')
                    found.Add(source[start..end]);

                index = end;
            }
        }

        return found;
    }

    private static EditorKind KindFromBaseType(string baseType)
    {
        if (string.IsNullOrWhiteSpace(baseType))
            return EditorKind.Unknown;

        var name = baseType.Contains('<') ? baseType[..baseType.IndexOf('<')] : baseType;

        foreach (var (suffix, kind) in EditorBaseSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return kind;
        }

        return EditorKind.Unknown;
    }

    private static IEnumerable<string> EnumerateSource(string sourceDirectory)
    {
        IEnumerable<string> files;

        try
        {
            files = Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (BuildOutputFilter.IsAnalyzable(file, sourceDirectory))
                yield return file;
        }
    }

    private static string SimpleAttributeName(string name)
    {
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
            name = name[(lastDot + 1)..];

        return name.EndsWith("Attribute", StringComparison.Ordinal)
            ? name[..^"Attribute".Length]
            : name;
    }

    private static string NamespaceOf(ClassDeclarationSyntax classDecl)
    {
        foreach (var ancestor in classDecl.Ancestors())
        {
            switch (ancestor)
            {
                case FileScopedNamespaceDeclarationSyntax fileScoped:
                    return fileScoped.Name.ToString();
                case NamespaceDeclarationSyntax declared:
                    return declared.Name.ToString();
            }
        }

        return string.Empty;
    }

    private static string? SummaryOf(ClassDeclarationSyntax classDecl)
    {
        var trivia = classDecl.GetLeadingTrivia().ToFullString();

        var lines = trivia
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("///", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('<'))
            .ToList();

        return lines.Count == 0 ? null : string.Join(" ", lines);
    }
}
