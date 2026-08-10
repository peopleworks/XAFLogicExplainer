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

        return types.Distinct().ToList();
    }

    /// <summary>
    /// Extracts required module references from module members.
    /// </summary>
    private static List<string> ExtractRequiredModules(ClassDeclarationSyntax classDecl)
    {
        var modules = new List<string>();

        // Find AdditionalExportedTypes or RequiredModuleTypes overrides
        var methods = classDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.Body == null) continue;

            var typeofExpressions = method.Body.DescendantNodes()
                .OfType<TypeOfExpressionSyntax>()
                .Select(t => t.Type.ToString())
                .Where(t => t.Contains("Module"));

            modules.AddRange(typeofExpressions);
        }

        // Check constructor for RequiredModuleTypes.Add(typeof(...))
        var ctors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var ctor in ctors)
        {
            if (ctor.Body == null) continue;

            // Find .Add(typeof(SomeModule)) invocations
            var invocations = ctor.Body.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(i => i.Expression.ToString().Contains("RequiredModuleTypes")
                             || i.Expression.ToString().Contains("Add"));

            foreach (var invocation in invocations)
            {
                var typeofArgs = invocation.DescendantNodes()
                    .OfType<TypeOfExpressionSyntax>()
                    .Select(t => t.Type.ToString());

                modules.AddRange(typeofArgs);
            }
        }

        return modules.Distinct().ToList();
    }

    /// <summary>
    /// Locates the most likely module file within the source tree.
    /// </summary>
    private static string? FindModuleFile(string sourceDirectory)
    {
        var candidate = Path.Combine(sourceDirectory, "Module.cs");
        if (File.Exists(candidate)) return candidate;

        return Directory.GetFiles(sourceDirectory, "Module.cs", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains("obj") && !f.Contains("bin"));
    }
}
