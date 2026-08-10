using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Extracts module-level metadata such as exported types and required modules.
/// </summary>
public class ModuleAnalyzer
{
    /// <summary>
    /// Parses the module class in a source directory and returns extracted metadata.
    /// </summary>
    /// <param name="sourceDirectory">Project source root.</param>
    /// <param name="options">Extraction options (reserved for future use).</param>
    /// <returns>Module metadata or null when no module class is found.</returns>
    public ExtractedModuleInfo? AnalyzeModule(string sourceDirectory, ExtractionOptions options)
    {
        var moduleFile = FindModuleFile(sourceDirectory);
        if (moduleFile == null) return null;

        var source = File.ReadAllText(moduleFile);
        var tree = CSharpSyntaxTree.ParseText(source, path: moduleFile);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.BaseList?.Types.Any(t =>
                t.Type.ToString().Contains("ModuleBase")) == true);

        if (classDecl == null) return null;

        var moduleInfo = new ExtractedModuleInfo
        {
            ModuleClassName = classDecl.Identifier.Text,
        };

        // Extract registered types from GetExportedTypes or Setup method
        moduleInfo.RegisteredTypes.AddRange(ExtractRegisteredTypes(classDecl));

        // Extract required modules from RequiredModuleTypes or Setup
        moduleInfo.RequiredModules.AddRange(ExtractRequiredModules(classDecl));

        return moduleInfo;
    }

    /// <summary>
    /// Extracts registered/exported type references from module members.
    /// </summary>
    private static List<string> ExtractRegisteredTypes(ClassDeclarationSyntax classDecl)
    {
        var types = new List<string>();

        // Find methods that register types (GetExportedTypes, Setup, CustomizeTypesInfo)
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "GetExportedTypes" or "Setup" or "CustomizeTypesInfo");

        foreach (var method in methods)
        {
            if (method.Body == null) continue;

            // Find typeof(EntityName) expressions
            var typeofExpressions = method.Body.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => t.Type.ToString());

            types.AddRange(typeofExpressions);
        }

        // Also check property-style overrides
        var properties = classDecl.Members.OfType<PropertyDeclarationSyntax>()
            .Where(p => p.Identifier.Text == "ExportedTypeAssemblies" || p.Identifier.Text == "DeclaredExportedTypes");

        foreach (var prop in properties)
        {
            var typeofExpressions = prop.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => t.Type.ToString());

            types.AddRange(typeofExpressions);
        }

        // Check field initializers or array/collection initializers containing typeof
        var fieldDecls = classDecl.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fieldDecls)
        {
            var typeofExpressions = field.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => t.Type.ToString());

            types.AddRange(typeofExpressions);
        }

        // AdditionalExportedTypes.Add(typeof(X)) is the idiomatic place to export a business
        // class, and it is written in the constructor -- which none of the scans above look at.
        types.AddRange(CollectAddedTypes(classDecl, "AdditionalExportedTypes"));

        return types.Distinct().ToList();
    }

    /// <summary>
    /// Extracts required module references from module members.
    /// </summary>
    private static List<string> ExtractRequiredModules(ClassDeclarationSyntax classDecl) =>
        CollectAddedTypes(classDecl, "RequiredModuleTypes");

    /// <summary>
    /// Collects the <c>typeof(...)</c> arguments passed to <c>&lt;collection&gt;.Add(...)</c>
    /// anywhere in a class.
    /// </summary>
    /// <remarks>
    /// The collection name has to be matched on the invocation target, not merely present
    /// somewhere in the class. An earlier version accepted any invocation whose expression
    /// contained "Add", which matched every <c>.Add()</c> call in the constructor — so
    /// <c>AdditionalExportedTypes.Add(typeof(Cliente))</c> registered a business entity as a
    /// required XAF module. Being listed under the wrong heading in generated documentation is
    /// worse than being absent, because it reads as authoritative.
    /// </remarks>
    /// <param name="classDecl">The module class.</param>
    /// <param name="collectionName">Collection being added to, e.g. <c>RequiredModuleTypes</c>.</param>
    private static List<string> CollectAddedTypes(ClassDeclarationSyntax classDecl, string collectionName)
    {
        var results = new List<string>();

        var invocations = classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            // The call must be `<something>.<collectionName>.Add(...)`, so inspect the member
            // access itself rather than searching the rendered expression for a substring.
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.Text: "Add" } add)
                continue;

            var target = add.Expression.ToString();
            var isTargetCollection =
                target.Equals(collectionName, StringComparison.Ordinal) ||
                target.EndsWith($".{collectionName}", StringComparison.Ordinal);

            if (!isTargetCollection)
                continue;

            results.AddRange(invocation.ArgumentList.Arguments
                .Select(a => a.Expression)
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => t.Type.ToString()));
        }

        return results.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Locates the most likely module file within the source tree.
    /// </summary>
    private static string? FindModuleFile(string sourceDirectory)
    {
        var candidate = Path.Combine(sourceDirectory, "Module.cs");
        if (File.Exists(candidate)) return candidate;

        return Directory.GetFiles(sourceDirectory, "Module.cs", SearchOption.AllDirectories)
            .FirstOrDefault(f => BuildOutputFilter.IsAnalyzable(f, sourceDirectory));
    }
}
