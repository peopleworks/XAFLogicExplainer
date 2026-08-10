using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Extracts seed-data creation logic from XAF updater classes.
/// </summary>
public class UpdaterAnalyzer
{
    /// <summary>
    /// Finds and analyzes updater methods that create initial data.
    /// </summary>
    /// <param name="sourceDirectory">Project source root.</param>
    /// <param name="options">Extraction options controlling output detail.</param>
    /// <returns>Extracted seed-data descriptors.</returns>
    public List<ExtractedSeedData> AnalyzeUpdater(string sourceDirectory, ExtractionOptions options)
    {
        var seedData = new List<ExtractedSeedData>();
        var updaterFile = FindUpdaterFile(sourceDirectory, options);

        if (updaterFile == null) return seedData;

        var source = File.ReadAllText(updaterFile);
        var tree = CSharpSyntaxTree.ParseText(source, path: updaterFile);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.Text == "Updater"
                                  || c.BaseList?.Types.Any(t => t.Type.ToString().Contains("ModuleUpdater")) == true);

        if (classDecl == null) return seedData;

        // Find all methods that create seed data
        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodName = method.Identifier.Text;

            // Skip standard XAF methods that aren't seed data
            if (methodName is "UpdateDatabaseAfterUpdateSchema" or "UpdateDatabaseBeforeUpdateSchema")
            {
                // But analyze their body for method calls to seed methods
                if (method.Body != null)
                {
                    AnalyzeUpdateMethod(classDecl, method.Body, seedData, options);
                }
                continue;
            }

            // Analyze methods that create objects
            if (method.Body != null && HasObjectCreation(method.Body))
            {
                var seed = ExtractSeedFromMethod(method, options);
                if (seed != null && seed.Records.Count > 0)
                    seedData.Add(seed);
            }
        }

        return seedData;
    }

    /// <summary>
    /// Traverses update entry points to find delegated seed methods.
    /// </summary>
    private static void AnalyzeUpdateMethod(ClassDeclarationSyntax classDecl, BlockSyntax body, List<ExtractedSeedData> seedData, ExtractionOptions options)
    {
        // Find method invocations within UpdateDatabaseAfterUpdateSchema
        var invocations = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(i => i.Expression.ToString())
            .Distinct();

        foreach (var invocation in invocations)
        {
            var methodName = invocation.Contains('.') ? invocation.Split('.').Last() : invocation;

            var targetMethod = classDecl.Members.OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == methodName);

            if (targetMethod?.Body != null && HasObjectCreation(targetMethod.Body))
            {
                var seed = ExtractSeedFromMethod(targetMethod, options);
                if (seed != null && seed.Records.Count > 0)
                    seedData.Add(seed);
            }
        }
    }

    /// <summary>
    /// Extracts one seed-data block from a method body.
    /// </summary>
    private static ExtractedSeedData? ExtractSeedFromMethod(MethodDeclarationSyntax method, ExtractionOptions options)
    {
        if (method.Body == null) return null;

        var seed = new ExtractedSeedData
        {
            MethodName = method.Identifier.Text,
            Description = InferDescriptionFromMethodName(method.Identifier.Text),
        };

        if (options.IncludeSourceCode)
            seed.RawSourceCode = method.Body.ToString();

        // Find object creation expressions (new TipoEmpleado(session) { ... })
        var objectCreations = method.Body.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>();

        foreach (var creation in objectCreations)
        {
            var typeName = creation.Type.ToString();

            // Skip infrastructure types
            if (typeName.Contains("PermissionPolicy") || typeName.Contains("ApplicationUser"))
            {
                seed.EntityType = typeName;
                continue;
            }

            seed.EntityType = typeName;

            if (creation.Initializer != null)
            {
                var record = new SeedRecord();
                foreach (var expression in creation.Initializer.Expressions)
                {
                    if (expression is AssignmentExpressionSyntax assignment)
                    {
                        var propName = assignment.Left.ToString();
                        var propValue = assignment.Right.ToString().Trim('"');
                        record.PropertyValues[propName] = propValue;
                    }
                }
                if (record.PropertyValues.Count > 0)
                    seed.Records.Add(record);
            }

            // Also check for property assignments after creation: var x = new Type(); x.Prop = value;
            var assignmentParent = creation.Ancestors().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
            var equalsClause = creation.Ancestors().OfType<EqualsValueClauseSyntax>().FirstOrDefault();

            string? variableName = null;
            if (assignmentParent != null)
            {
                variableName = assignmentParent.Declaration.Variables.FirstOrDefault()?.Identifier.Text;
            }
            else
            {
                var assignExpr = creation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
                if (assignExpr != null)
                    variableName = assignExpr.Left.ToString();
            }

            if (variableName != null)
            {
                var record = seed.Records.LastOrDefault() ?? new SeedRecord();
                if (!seed.Records.Contains(record))
                    seed.Records.Add(record);

                // Find subsequent property assignments
                var block = method.Body;
                var assignments = block.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Where(a => a.Left is MemberAccessExpressionSyntax mae
                                && mae.Expression.ToString() == variableName);

                foreach (var assignment in assignments)
                {
                    if (assignment.Left is MemberAccessExpressionSyntax mae)
                    {
                        record.PropertyValues[mae.Name.ToString()] = assignment.Right.ToString().Trim('"');
                    }
                }
            }
        }

        return seed;
    }

    /// <summary>
    /// Checks whether a method body contains object creation expressions.
    /// </summary>
    private static bool HasObjectCreation(BlockSyntax body)
    {
        return body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any();
    }

    /// <summary>
    /// Converts a method identifier into a readable seed-data description.
    /// </summary>
    private static string InferDescriptionFromMethodName(string methodName)
    {
        // Convert "CrearTiposEmpleado" -> "Crear Tipos de Empleado"
        var result = System.Text.RegularExpressions.Regex.Replace(methodName, "([A-Z])", " $1").Trim();
        return $"Seed data: {result}";
    }

    /// <summary>
    /// Locates the updater file in common XAF locations.
    /// </summary>
    private static string? FindUpdaterFile(string sourceDirectory, ExtractionOptions options)
    {
        // Search for Updater.cs in DatabaseUpdate folder or project root
        var candidates = new[]
        {
            Path.Combine(sourceDirectory, "DatabaseUpdate", "Updater.cs"),
            Path.Combine(sourceDirectory, "Updater.cs"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        // Fallback: search all directories
        return Directory.GetFiles(sourceDirectory, "Updater.cs", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.Contains("obj") && !f.Contains("bin"));
    }
}
