using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Reads what a report shows from wherever its layout is kept.
/// </summary>
/// <remarks>
/// Three places, one vocabulary. Visual Studio's designer writes <c>InitializeComponent</c> in a
/// <c>*.Designer.cs</c>; the End-User Designer, and an export from the running application, write
/// a <c>.repx</c> whose element and attribute names mirror the designer's property names; and a
/// quick report is built by hand in its constructor. All three set the same things —
/// <c>FilterString</c>, <c>ExpressionBindings</c>, <c>GroupFields</c>, <c>CalculatedFields</c>,
/// <c>Parameters</c>, a data source — so they are read into one model.
/// <para>
/// The code reader never resolves a type: it records which identifier was assigned <c>new T()</c>
/// and reads the properties later assigned on that identifier. That is enough for designer output,
/// which is regular to the point of being generated, and for the constructor shape, and it is the
/// only thing available without a compilation.
/// </para>
/// </remarks>
internal static class ReportLayoutReader
{
    /// <summary>
    /// The layout of the report class named <paramref name="reportType"/>, searched for under the
    /// directory; null when the class is not declared there.
    /// </summary>
    public static ReportLayout? ForReportType(string reportType, string sourceDirectory, IReadOnlyList<string> files)
    {
        var parts = new List<(ClassDeclarationSyntax Class, string File)>();

        foreach (var file in files.Where(f => ContainsWord(f, reportType)))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot();

            parts.AddRange(root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.Identifier.Text == reportType)
                .Select(c => (c, file)));
        }

        if (parts.Count == 0)
            return null;

        // A class that loads a .repx is described by the .repx, whatever else its code says.
        var repx = parts
            .SelectMany(p => p.Class.DescendantTokens().Where(t => t.IsKind(SyntaxKind.StringLiteralToken)))
            .Select(t => t.ValueText)
            .FirstOrDefault(v => v.EndsWith(".repx", StringComparison.OrdinalIgnoreCase));

        if (repx != null && FindRepx(repx, sourceDirectory) is { } repxFile && FromRepx(repxFile) is { } fromRepx)
            return fromRepx;

        return FromCode(parts);
    }

    /// <summary>
    /// A <c>.repx</c> resource name — <c>Invoicing.Module.Reports.CustomerStatement.repx</c> — or a
    /// path; either way the file name is the last segment, and the file is looked up by that.
    /// </summary>
    private static string? FindRepx(string reference, string sourceDirectory)
    {
        var fileName = Path.GetFileName(reference.Replace('\\', '/'));
        var stem = Path.GetFileNameWithoutExtension(fileName);
        // A resource name's "file name" is everything after the last dot before .repx.
        var lastDot = stem.LastIndexOf('.');
        if (lastDot >= 0)
            fileName = stem[(lastDot + 1)..] + ".repx";

        return RepxFiles(sourceDirectory)
            .FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every <c>.repx</c> in the solution: under the module, and under anything beside it.
    /// </summary>
    /// <remarks>
    /// Beside it, because that is where they are. A shop that designs reports in the End-User
    /// Designer keeps the exports in a <c>Reporting</c> folder at the solution root — fourteen of
    /// them in one production application, and none under the module the extractor is pointed at.
    /// The same parent-directory reach the editor and xafml discovery already use.
    /// </remarks>
    public static IEnumerable<string> RepxFiles(string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
            return [];

        var root = Directory.GetParent(sourceDirectory)?.FullName ?? sourceDirectory;

        return Directory.GetFiles(root, "*.repx", SearchOption.AllDirectories)
            .Where(f => BuildOutputFilter.IsAnalyzable(f, root))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    // ---------------------------------------------------------------- code

    private static ReportLayout FromCode(List<(ClassDeclarationSyntax Class, string File)> parts)
    {
        var designer = parts.FirstOrDefault(p => p.Class.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == "InitializeComponent"));

        var home = designer.Class != null ? designer : parts[0];

        var layout = new ReportLayout
        {
            Source = designer.Class != null ? ReportLayoutSource.DesignerCode : ReportLayoutSource.Code,
            FilePath = home.File,
            Line = SourceLine.Of(home.Class.Identifier),
        };

        // Identifier -> the type it was given with `new T(...)` or declared as, and the properties
        // assigned on it, in source order.
        var components = new Dictionary<string, Component>(StringComparer.Ordinal);
        var order = new List<string>();

        Component Of(string name)
        {
            if (!components.TryGetValue(name, out var component))
            {
                component = new Component();
                components[name] = component;
                order.Add(name);
            }
            return component;
        }

        foreach (var (cls, _) in parts)
        {
            foreach (var field in cls.Members.OfType<FieldDeclarationSyntax>())
            foreach (var variable in field.Declaration.Variables)
                Of(variable.Identifier.Text).Type ??= ShortName(field.Declaration.Type);

            foreach (var node in cls.DescendantNodes())
            {
                switch (node)
                {
                    // this.x = new T(); x = new T();
                    case AssignmentExpressionSyntax { Right: ObjectCreationExpressionSyntax creation } assignment
                        when ReceiverName(assignment.Left) is { } created:
                        Of(created).Type = ShortName(creation.Type);
                        ReadInitializer(creation, Of(created));
                        break;

                    // var x = new T { ... };
                    case VariableDeclaratorSyntax { Initializer.Value: ObjectCreationExpressionSyntax creation } declarator:
                        Of(declarator.Identifier.Text).Type = ShortName(creation.Type);
                        ReadInitializer(creation, Of(declarator.Identifier.Text));
                        break;

                    // this.x.Prop = value; x.Prop = value; Prop = value (report-level)
                    case AssignmentExpressionSyntax assignment
                        when PropertyTarget(assignment.Left) is ({ } receiver, { } property):
                        Of(receiver).Properties[property] = assignment.Right;
                        break;

                    case ObjectCreationExpressionSyntax creation when ShortName(creation.Type) == "ExpressionBinding":
                        if (ReadBinding(creation) is { } binding)
                            layout.Bindings.Add(binding);
                        break;

                    case ObjectCreationExpressionSyntax creation when ShortName(creation.Type) == "GroupField":
                        if (creation.ArgumentList?.Arguments.Count > 0)
                            layout.GroupFields.Add(SyntaxLiteral.ValueOf(creation.ArgumentList.Arguments[0].Expression));
                        break;
                }
            }
        }

        var report = components.GetValueOrDefault("this");

        layout.FilterString = report?.Text("FilterString");
        layout.DataMember = report?.Text("DataMember");

        // The data source: what `DataSource = …` names, or failing that the first data source
        // component declared.
        var sourceName = report?.Properties.GetValueOrDefault("DataSource") is { } dataSource
            ? ReceiverName(dataSource)
            : order.FirstOrDefault(n => components[n].Type is { } t && t.EndsWith("DataSource", StringComparison.Ordinal));

        if (sourceName != null && components.TryGetValue(sourceName, out var source))
        {
            layout.DataSourceKind = source.Type;
            layout.DataSource = source.Properties.GetValueOrDefault("ObjectTypeName") is { } objectType
                ? ShortTypeName(objectType)
                : source.Text("ViewId") ?? source.Text("Name") ?? sourceName;
        }

        foreach (var name in order)
        {
            var component = components[name];
            switch (component.Type)
            {
                case "CalculatedField":
                    layout.CalculatedFields.Add(new ReportCalculatedField
                    {
                        Name = component.Text("Name") ?? name,
                        Expression = component.Text("Expression") ?? string.Empty,
                    });
                    break;

                case "Parameter":
                    layout.Parameters.Add(new ReportParameter
                    {
                        Name = component.Text("Name") ?? name,
                        Type = component.Properties.GetValueOrDefault("Type") is { } type ? ShortTypeName(type) : "String",
                        Visible = component.Properties.GetValueOrDefault("Visible") is { } visible ? Bool(visible) ?? true : true,
                        Description = component.Text("Description"),
                    });
                    break;
            }
        }

        return layout;
    }

    private sealed class Component
    {
        public string? Type;
        public Dictionary<string, ExpressionSyntax> Properties { get; } = new(StringComparer.Ordinal);

        public string? Text(string property) =>
            Properties.TryGetValue(property, out var value) ? SyntaxLiteral.ValueOf(value) : null;
    }

    private static void ReadInitializer(ObjectCreationExpressionSyntax creation, Component component)
    {
        foreach (var expression in creation.Initializer?.Expressions ?? default)
        {
            if (expression is AssignmentExpressionSyntax { Left: IdentifierNameSyntax property } assignment)
                component.Properties[property.Identifier.Text] = assignment.Right;
        }
    }

    /// <summary>
    /// <c>new ExpressionBinding("BeforePrint", "Text", "[Number]")</c> or
    /// <c>new ExpressionBinding("Text", "[Number]")</c>, owned by whichever control's
    /// <c>ExpressionBindings</c> the creation is being added to.
    /// </summary>
    private static ReportBinding? ReadBinding(ObjectCreationExpressionSyntax creation)
    {
        var args = creation.ArgumentList?.Arguments;
        if (args is not { Count: >= 2 })
            return null;

        var owner = creation.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Select(i => i.Expression as MemberAccessExpressionSyntax)
            .FirstOrDefault(m => m?.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ExpressionBindings" });

        return new ReportBinding
        {
            Control = owner?.Expression is MemberAccessExpressionSyntax bindings ? ReceiverName(bindings.Expression) ?? "" : "",
            Property = SyntaxLiteral.ValueOf(args.Value[^2].Expression),
            Expression = SyntaxLiteral.ValueOf(args.Value[^1].Expression),
        };
    }

    /// <summary><c>this.x.Prop</c> → (x, Prop); <c>x.Prop</c> → (x, Prop); <c>this.Prop</c> and <c>Prop</c> → (this, Prop).</summary>
    private static (string Receiver, string Property)? PropertyTarget(ExpressionSyntax left) => left switch
    {
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => ("this", access.Name.Identifier.Text),
        MemberAccessExpressionSyntax access when ReceiverName(access.Expression) is { } receiver => (receiver, access.Name.Identifier.Text),
        IdentifierNameSyntax identifier => ("this", identifier.Identifier.Text),
        _ => null,
    };

    /// <summary><c>this.x</c> → x; <c>x</c> → x; anything else → null.</summary>
    private static string? ReceiverName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } access => access.Name.Identifier.Text,
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        _ => null,
    };

    /// <summary><c>"A.B.Invoice"</c> → Invoice; <c>typeof(Invoice).FullName</c> → Invoice; <c>typeof(System.DateTime)</c> → DateTime.</summary>
    private static string ShortTypeName(ExpressionSyntax expression)
    {
        var typeOf = expression.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>().FirstOrDefault();
        if (typeOf != null)
            return ShortName(typeOf.Type);

        return AfterLastDot(SyntaxLiteral.ValueOf(expression));
    }

    private static bool? Bool(ExpressionSyntax expression) => expression.Kind() switch
    {
        SyntaxKind.TrueLiteralExpression => true,
        SyntaxKind.FalseLiteralExpression => false,
        _ => null,
    };

    private static string ShortName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.ToString(),
        // typeof(string) and a .repx's System.String are one type, and must read as one.
        PredefinedTypeSyntax predefined => predefined.Keyword.Text switch
        {
            "string" => "String", "bool" => "Boolean", "int" => "Int32", "long" => "Int64",
            "short" => "Int16", "byte" => "Byte", "decimal" => "Decimal", "double" => "Double",
            "float" => "Single", "char" => "Char", "object" => "Object",
            var other => other,
        },
        _ => type.ToString(),
    };

    private static string AfterLastDot(string name)
    {
        var comma = name.IndexOf(',');
        if (comma >= 0)
            name = name[..comma];
        var dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }

    // ---------------------------------------------------------------- repx

    /// <summary>Reads a <c>.repx</c>; null when the file is not the XML a report layout is.</summary>
    public static ReportLayout? FromRepx(string file)
    {
        XElement root;
        try
        {
            root = XDocument.Load(file).Root ?? throw new InvalidDataException("empty");
        }
        catch (Exception e) when (e is System.Xml.XmlException or IOException or InvalidDataException)
        {
            return null;
        }

        var layout = new ReportLayout
        {
            Source = ReportLayoutSource.Repx,
            FilePath = file,
            FilterString = (string?)root.Attribute("FilterString"),
            DataMember = (string?)root.Attribute("DataMember"),
        };

        // Type="#Ref-4" on a parameter points at <ObjectStorage><Item Ref="4" Content="System.Double"/>.
        var objectStorage = root.Element("ObjectStorage")?.Elements()
            .Where(e => e.Attribute("Ref") != null)
            .ToDictionary(e => (string)e.Attribute("Ref")!, e => (string?)e.Attribute("Content") ?? "", StringComparer.Ordinal)
            ?? [];

        // The designer writes the data source into <ComponentStorage>; an export from the running
        // application writes it into <ObjectStorage>. The #Ref is followed into both.
        var components = new[] { "ComponentStorage", "ObjectStorage" }
            .SelectMany(storage => root.Element(storage)?.Elements() ?? [])
            .ToList();
        var dataSourceRef = ((string?)root.Attribute("DataSource"))?.Replace("#Ref-", "");
        var dataSource = components.FirstOrDefault(c => (string?)c.Attribute("Ref") == dataSourceRef)
                         ?? components.FirstOrDefault(c => ((string?)c.Attribute("ObjectType") ?? "").Contains("DataSource", StringComparison.Ordinal));

        if (dataSource != null)
        {
            layout.DataSourceKind = AfterLastDot((string?)dataSource.Attribute("ObjectType") ?? "");
            layout.DataSource = (string?)dataSource.Attribute("ObjectTypeName") is { } objectType
                ? AfterLastDot(objectType)
                : (string?)dataSource.Attribute("ViewId") ?? (string?)dataSource.Attribute("Name");
        }

        foreach (var parameter in root.Element("Parameters")?.Elements() ?? [])
        {
            var typeRef = ((string?)parameter.Attribute("Type"))?.Replace("#Ref-", "");
            layout.Parameters.Add(new ReportParameter
            {
                Name = (string?)parameter.Attribute("Name") ?? "",
                Type = typeRef != null && objectStorage.TryGetValue(typeRef, out var content) ? AfterLastDot(content) : "String",
                Visible = (string?)parameter.Attribute("Visible") is { } visible ? bool.Parse(visible) : true,
                Description = (string?)parameter.Attribute("Description"),
            });
        }

        foreach (var field in root.Element("CalculatedFields")?.Elements() ?? [])
        {
            layout.CalculatedFields.Add(new ReportCalculatedField
            {
                Name = (string?)field.Attribute("Name") ?? "",
                Expression = (string?)field.Attribute("Expression") ?? "",
            });
        }

        foreach (var bindings in root.Descendants("ExpressionBindings"))
        foreach (var binding in bindings.Elements())
        {
            layout.Bindings.Add(new ReportBinding
            {
                Control = (string?)bindings.Parent?.Attribute("Name") ?? "",
                Property = (string?)binding.Attribute("PropertyName") ?? "",
                Expression = (string?)binding.Attribute("Expression") ?? "",
            });
        }

        foreach (var group in root.Descendants("GroupFields").Elements())
        {
            if ((string?)group.Attribute("FieldName") is { } fieldName)
                layout.GroupFields.Add(fieldName);
        }

        return layout;
    }

    private static bool ContainsWord(string file, string word)
    {
        try
        {
            return File.ReadAllText(file).Contains(word, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
