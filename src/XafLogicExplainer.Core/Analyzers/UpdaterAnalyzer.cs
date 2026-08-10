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
                    AddSeed(seedData, seed);
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
                    AddSeed(seedData, seed);
            }
        }
    }

    /// <summary>
    /// Adds a seed method unless it has already been recorded.
    /// </summary>
    /// <remarks>
    /// A seed method is reached twice: once by following the calls out of
    /// <c>UpdateDatabaseAfterUpdateSchema</c>, and once by the sweep over every method in the
    /// class. Without this guard each one is reported twice, so documentation claims an
    /// application seeds twice as many things as it does — and the duplicate is a perfect copy,
    /// which makes it read like two genuinely separate operations.
    /// </remarks>
    private static void AddSeed(List<ExtractedSeedData> seedData, ExtractedSeedData seed)
    {
        if (seedData.Any(existing => existing.MethodName == seed.MethodName))
            return;

        seedData.Add(seed);
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
                        var propValue = SyntaxLiteral.ValueOf(assignment.Right);
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
                        record.PropertyValues[mae.Name.ToString()] = SyntaxLiteral.ValueOf(assignment.Right);
                    }
                }
            }
        }

        ExtractObjectSpaceCreations(method.Body, seed);

        return seed;
    }

    /// <summary>
    /// Reads seed records created through <c>ObjectSpace.CreateObject&lt;T&gt;()</c>.
    /// </summary>
    /// <remarks>
    /// The scan above only recognizes <c>new Customer(session)</c>. That is the older
    /// Session-based style; a modern updater works against <c>IObjectSpace</c> and writes
    /// <c>ObjectSpace.CreateObject&lt;Customer&gt;()</c>, which is an invocation rather than an
    /// object creation and so was invisible. Such an application was reported as having no seed
    /// data at all — the tool describing the absence of something plainly present in the source.
    /// </remarks>
    private static void ExtractObjectSpaceCreations(BlockSyntax body, ExtractedSeedData seed)
    {
        var creations = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.Text: "CreateObject" }
            });

        foreach (var creation in creations)
        {
            var generic = (GenericNameSyntax)((MemberAccessExpressionSyntax)creation.Expression).Name;
            var typeName = generic.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();

            if (string.IsNullOrWhiteSpace(typeName))
                continue;

            seed.EntityType = typeName;

            // The result is either declared (var x = ...) or assigned to an existing local
            // (x = ...), and both forms appear in the same updater when a method checks for an
            // existing record before creating one.
            var variableName =
                creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.Text
                ?? (creation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault()?.Left.ToString());

            if (string.IsNullOrWhiteSpace(variableName))
                continue;

            var record = new SeedRecord();

            foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is MemberAccessExpressionSyntax member
                    && member.Expression.ToString() == variableName)
                {
                    record.PropertyValues[member.Name.ToString()] = SyntaxLiteral.ValueOf(assignment.Right);
                }
            }

            if (record.PropertyValues.Count > 0)
                seed.Records.Add(record);
        }
    }

    /// <summary>
    /// Checks whether a method body creates persistent objects, in either supported style.
    /// </summary>
    private static bool HasObjectCreation(BlockSyntax body)
    {
        if (body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any())
            return true;

        return body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(i => i.Expression is MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.Text: "CreateObject" }
            });
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
    /// Locates the module updater.
    /// </summary>
    /// <remarks>
    /// The XAF project template produces <c>DatabaseUpdate/Updater.cs</c>, so that is checked
    /// first and costs nothing. Searching only for that file name was the whole strategy, though,
    /// which meant a class named anything else — <c>SeedDataUpdater</c>, <c>DemoDataUpdater</c>,
    /// or an updater split per area — made the tool report an application with no seed data at
    /// all, silently. The final fallback looks for what actually matters: a class deriving from
    /// <c>ModuleUpdater</c>.
    /// </remarks>
    private static string? FindUpdaterFile(string sourceDirectory, ExtractionOptions options)
    {
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

        var byName = Directory.GetFiles(sourceDirectory, "Updater.cs", SearchOption.AllDirectories)
            .FirstOrDefault(f => BuildOutputFilter.IsAnalyzable(f, sourceDirectory));

        if (byName != null)
            return byName;

        // Read files rather than trusting their names. Bounded by the project directory, and only
        // reached when the conventional locations came up empty.
        //
        // The check parses for a class that actually derives from ModuleUpdater. Searching the
        // text for "ModuleUpdater" instead picks the module itself, because every module declares
        // `IEnumerable<ModuleUpdater> GetModuleUpdaters(...)` -- and the module has no updater
        // class in it, so the search would end on a file guaranteed to yield nothing.
        foreach (var file in Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (!BuildOutputFilter.IsAnalyzable(file, sourceDirectory))
                continue;

            try
            {
                var source = File.ReadAllText(file);

                // Cheap gate before parsing: a file without the word cannot declare the class.
                if (!source.Contains("ModuleUpdater", StringComparison.Ordinal))
                    continue;

                var declaresUpdater = CSharpSyntaxTree.ParseText(source)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Any(c => c.BaseList?.Types.Any(t =>
                        t.Type.ToString().EndsWith("ModuleUpdater", StringComparison.Ordinal)) == true);

                if (declaresUpdater)
                    return file;
            }
            catch (IOException)
            {
                // An unreadable file is not a reason to abandon the search.
            }
        }

        return null;
    }
}
