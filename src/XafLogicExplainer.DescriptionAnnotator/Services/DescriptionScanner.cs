using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XafLogicExplainer.DescriptionAnnotator.Services;

/// <summary>
/// Scans C# business objects and reports classes/properties missing <c>[Description]</c> attributes.
/// </summary>
public class DescriptionScanner
{
    /// <summary>
    /// Scan a project directory for classes and properties missing [Description] attributes.
    /// </summary>
    public ScanResult Scan(string projectPath, string[] baseTypeNames)
    {
        var result = new ScanResult { ProjectPath = projectPath };

        var boDir = Path.Combine(projectPath, "BusinessObjects");
        var searchDir = Directory.Exists(boDir) ? boDir : projectPath;

        var csFiles = Directory.GetFiles(searchDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));

        foreach (var file in csFiles)
        {
            var source = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(source, path: file);
            var root = tree.GetRoot();

            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (!IsXafBusinessObject(classDecl, baseTypeNames))
                    continue;

                var className = classDecl.Identifier.Text;
                var hasClassDescription = HasDescriptionAttribute(classDecl);

                var classItem = new MissingDescriptionItem
                {
                    FilePath = file,
                    ClassName = className,
                    MemberName = null,
                    MemberType = "class",
                    TypeName = GetBaseTypeName(classDecl),
                    HasDescription = hasClassDescription,
                    ExistingDescription = hasClassDescription ? GetDescriptionValue(classDecl) : null,
                    LineNumber = classDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    Context = BuildClassContext(classDecl)
                };

                result.AllItems.Add(classItem);
                if (!hasClassDescription)
                    result.MissingItems.Add(classItem);

                // Scan properties
                foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
                {
                    var propName = prop.Identifier.Text;
                    if (IsInfrastructureProperty(propName)) continue;

                    var hasPropDescription = HasDescriptionAttribute(prop);

                    var propItem = new MissingDescriptionItem
                    {
                        FilePath = file,
                        ClassName = className,
                        MemberName = propName,
                        MemberType = "property",
                        TypeName = prop.Type.ToString(),
                        HasDescription = hasPropDescription,
                        ExistingDescription = hasPropDescription ? GetDescriptionValue(prop) : null,
                        LineNumber = prop.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        Context = BuildPropertyContext(classDecl, prop)
                    };

                    result.AllItems.Add(propItem);
                    if (!hasPropDescription)
                        result.MissingItems.Add(propItem);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Builds class-level AI context from declaration metadata.
    /// </summary>
    private static ClassContext BuildClassContext(ClassDeclarationSyntax classDecl)
    {
        var ctx = new ClassContext
        {
            ClassName = classDecl.Identifier.Text,
            BaseType = GetBaseTypeName(classDecl),
            NavigationGroup = GetAttributeArg(classDecl, "NavigationItem"),
            DefaultProperty = GetAttributeArg(classDecl, "XafDefaultProperty")
                              ?? GetAttributeArg(classDecl, "DefaultProperty"),
        };

        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            var name = prop.Identifier.Text;
            if (IsInfrastructureProperty(name)) continue;
            ctx.PropertyNames.Add($"{name} ({prop.Type})");
        }

        // Collect association names for relationship context
        foreach (var prop in classDecl.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (HasAttribute(prop, "Association"))
            {
                var typeName = prop.Type.ToString();
                if (typeName.Contains("XPCollection") || typeName.Contains("IList"))
                    ctx.RelatedEntities.Add($"{prop.Identifier.Text} -> many");
                else
                    ctx.RelatedEntities.Add($"{prop.Identifier.Text} -> {typeName}");
            }
        }

        return ctx;
    }

    /// <summary>
    /// Builds property-level AI context enriched with class metadata.
    /// </summary>
    private static ClassContext BuildPropertyContext(ClassDeclarationSyntax classDecl, PropertyDeclarationSyntax prop)
    {
        var ctx = BuildClassContext(classDecl);
        ctx.TargetPropertyName = prop.Identifier.Text;
        ctx.TargetPropertyType = prop.Type.ToString();
        ctx.IsComputed = !prop.AccessorList?.Accessors.Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration) ?? prop.ExpressionBody != null;
        ctx.IsCollection = prop.Type.ToString().Contains("XPCollection") || prop.Type.ToString().Contains("IList");

        var size = GetAttributeArg(prop, "Size");
        if (size != null) ctx.Size = size;

        return ctx;
    }

    #region Helpers

    private static bool IsXafBusinessObject(ClassDeclarationSyntax classDecl, string[] baseTypeNames)
    {
        if (classDecl.BaseList == null) return false;
        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            var simple = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;
            if (baseTypeNames.Any(bt => simple.Equals(bt, StringComparison.Ordinal) || simple.EndsWith($".{bt}")))
                return true;
        }
        return false;
    }

    private static bool HasDescriptionAttribute(MemberDeclarationSyntax member)
    {
        return member.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "Description" or "System.ComponentModel.Description");
    }

    private static bool HasAttribute(MemberDeclarationSyntax member, string name)
    {
        return member.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Replace("Attribute", "") == name);
    }

    private static string? GetDescriptionValue(MemberDeclarationSyntax member)
    {
        var attr = member.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Description" or "System.ComponentModel.Description");

        if (attr?.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0)
            return null;

        return attr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
    }

    private static string? GetAttributeArg(MemberDeclarationSyntax member, string attrName)
    {
        var attr = member.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a =>
            {
                var n = a.Name.ToString().Replace("Attribute", "");
                return n == attrName || n.EndsWith($".{attrName}");
            });
        if (attr?.ArgumentList == null || attr.ArgumentList.Arguments.Count == 0) return null;
        return attr.ArgumentList.Arguments[0].Expression.ToString().Trim('"');
    }

    private static string GetBaseTypeName(ClassDeclarationSyntax classDecl)
        => classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? "object";

    private static bool IsInfrastructureProperty(string name)
        => name is "Session" or "ClassInfo" or "This" or "Loading" or "IsLoading"
            or "IsDeleted" or "IsSaving" or "Oid" or "GCRecord" or "OptimisticLockField";

    #endregion
}

/// <summary>
/// Result payload for a description scan operation.
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Project root path that was scanned.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// All discovered class/property candidates.
    /// </summary>
    public List<MissingDescriptionItem> AllItems { get; set; } = [];

    /// <summary>
    /// Subset of <see cref="AllItems"/> missing descriptions.
    /// </summary>
    public List<MissingDescriptionItem> MissingItems { get; set; } = [];

    /// <summary>
    /// Total class count found in scan set.
    /// </summary>
    public int TotalClasses => AllItems.Count(i => i.MemberType == "class");

    /// <summary>
    /// Total property count found in scan set.
    /// </summary>
    public int TotalProperties => AllItems.Count(i => i.MemberType == "property");

    /// <summary>
    /// Number of classes missing a description attribute.
    /// </summary>
    public int MissingClassDescriptions => MissingItems.Count(i => i.MemberType == "class");

    /// <summary>
    /// Number of properties missing a description attribute.
    /// </summary>
    public int MissingPropertyDescriptions => MissingItems.Count(i => i.MemberType == "property");
}

/// <summary>
/// Represents one class/property that may require a description attribute.
/// </summary>
public class MissingDescriptionItem
{
    /// <summary>
    /// Physical source file path.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Declaring class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Member name when item represents a property; null for class-level entries.
    /// </summary>
    public string? MemberName { get; set; }

    /// <summary>
    /// Entry category ("class" or "property").
    /// </summary>
    public string MemberType { get; set; } = string.Empty;

    /// <summary>
    /// Member type information for prompt context.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether a description already existed.
    /// </summary>
    public bool HasDescription { get; set; }

    /// <summary>
    /// Existing description value when present.
    /// </summary>
    public string? ExistingDescription { get; set; }

    /// <summary>
    /// Source line number of the declaration.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// AI prompt context for class/property semantics.
    /// </summary>
    public ClassContext Context { get; set; } = new();

    /// <summary>
    /// AI-generated suggestion for description text.
    /// </summary>
    public string? SuggestedDescription { get; set; }
}

/// <summary>
/// Context payload used to guide AI description generation.
/// </summary>
public class ClassContext
{
    public string ClassName { get; set; } = string.Empty;
    public string BaseType { get; set; } = string.Empty;
    public string? NavigationGroup { get; set; }
    public string? DefaultProperty { get; set; }
    public List<string> PropertyNames { get; set; } = [];
    public List<string> RelatedEntities { get; set; } = [];
    public string? TargetPropertyName { get; set; }
    public string? TargetPropertyType { get; set; }
    public bool IsComputed { get; set; }
    public bool IsCollection { get; set; }
    public string? Size { get; set; }
}
