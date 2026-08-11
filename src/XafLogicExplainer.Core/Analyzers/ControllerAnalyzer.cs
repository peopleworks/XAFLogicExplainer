using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Interfaces;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Parses XAF controllers and extracts actions, targets, methods, and references.
/// </summary>
public class ControllerAnalyzer : IControllerAnalyzer
{
    /// <summary>
    /// Scans all controller files and extracts controller metadata.
    /// </summary>
    /// <param name="sourceDirectory">Project source root directory.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>List of extracted controllers.</returns>
    public List<ExtractedController> AnalyzeControllers(string sourceDirectory, ExtractionOptions options)
    {
        var controllerDir = Path.Combine(sourceDirectory, "Controllers");

        if (!Directory.Exists(controllerDir))
        {
            // Fallback: search entire project for *Controller.cs files
            controllerDir = sourceDirectory;
        }

        var files = Directory.GetFiles(controllerDir, "*.cs", SearchOption.AllDirectories)
            .Where(file => BuildOutputFilter.IsAnalyzable(file, controllerDir));

        return Build(files.SelectMany(ReadClasses).ToList(), options);
    }

    /// <summary>
    /// Analyzes a single controller file.
    /// </summary>
    /// <remarks>
    /// A file on its own cannot see the rest of the project, so a controller whose base class lives
    /// in another file is recognised here only when the catalog or the naming convention identifies
    /// it. <see cref="AnalyzeControllers"/> closes over the whole tree and does not have that limit.
    /// </remarks>
    public List<ExtractedController> AnalyzeControllerFile(string filePath, ExtractionOptions options) =>
        Build(ReadClasses(filePath), options);

    /// <summary>
    /// Turns the classes found in a source tree into controllers.
    /// </summary>
    private static List<ExtractedController> Build(List<ClassCandidate> candidates, ExtractionOptions options)
    {
        var controllers = SelectControllers(candidates, options);

        return
        [
            .. candidates
                .Where(candidate => controllers.Contains(candidate.ClassName))
                // Every declaration of one class, so a partial split across files -- the shape the
                // XAF designer generates -- is read as the one controller it is rather than as two.
                .GroupBy(candidate => candidate.FullName, StringComparer.Ordinal)
                .Select(group => Compose(group, options)),
        ];
    }

    /// <summary>
    /// Reads every class declared in one file.
    /// </summary>
    private static List<ClassCandidate> ReadClasses(string filePath)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(filePath), path: filePath).GetRoot();

        // Whether the file imports XAF at all. It is what separates `HomeController : Controller`,
        // an ASP.NET controller that can sit in the same platform project, from an XAF one.
        var importsXaf = root is CompilationUnitSyntax unit
            && unit.Usings.Any(directive =>
                directive.Name?.ToString().StartsWith("DevExpress.ExpressApp", StringComparison.Ordinal) == true);

        return
        [
            .. root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(declaration => new ClassCandidate(filePath, declaration, importsXaf)),
        ];
    }

    /// <summary>
    /// Decides which classes are XAF controllers, following base classes to a fixed point.
    /// </summary>
    /// <remarks>
    /// A single pass over a whitelist of base type names finds only controllers that derive
    /// <em>directly</em> from <c>ViewController</c> and its two siblings. Real XAF code does not
    /// look like that: it extends shipped controllers (<c>ArchiveController :
    /// DeleteObjectsViewController</c>) and its own base classes, and every one of those was being
    /// dropped from the extraction entirely — silently, because a controller that is never seen
    /// cannot be reported as missing.
    /// </remarks>
    private static HashSet<string> SelectControllers(List<ClassCandidate> candidates, ExtractionOptions options)
    {
        var accepted = new HashSet<string>(StringComparer.Ordinal);
        var pending = candidates.Where(candidate => candidate.BaseType.Length > 0).ToList();

        // Each round can accept a class whose base was accepted in the previous one, so it repeats
        // until a round changes nothing. Bounded by the number of classes.
        bool changed;

        do
        {
            changed = false;

            for (var index = pending.Count - 1; index >= 0; index--)
            {
                if (!IsXafController(pending[index], accepted, options))
                    continue;

                accepted.Add(pending[index].ClassName);
                pending.RemoveAt(index);
                changed = true;
            }
        }
        while (changed);

        return accepted;
    }

    /// <summary>
    /// Builds one controller from every declaration of one class.
    /// </summary>
    private static ExtractedController Compose(IEnumerable<ClassCandidate> declarations, ExtractionOptions options)
    {
        var parts = declarations.ToList();
        var first = parts[0];

        var controller = new ExtractedController
        {
            ClassName = first.ClassName,
            Namespace = first.Namespace,
            FilePath = first.FilePath,
            BaseControllerType = parts.Select(part => part.BaseType).FirstOrDefault(name => name.Length > 0) ?? "object",
            // XAF only registers what it can instantiate, so an abstract base class never activates
            // on anything -- it hands its targeting and its actions down to the classes that do.
            IsAbstract = parts.Exists(part => part.Declaration.Modifiers.Any(SyntaxKind.AbstractKeyword)),
        };

        foreach (var part in parts)
        {
            var declaration = part.Declaration;

            ControllerTargetingReader.Merge(controller.Targeting, ControllerTargetingReader.Read(declaration));

            foreach (var action in ExtractActions(declaration, options))
            {
                if (!controller.Actions.Exists(existing => existing.ActionId == action.ActionId))
                    controller.Actions.Add(action);
            }

            if (options.IncludeMethodBodies)
            {
                foreach (var method in ExtractMethods(declaration))
                {
                    if (!controller.Methods.Exists(existing => existing.Name == method.Name))
                        controller.Methods.Add(method);
                }
            }

            Absorb(controller.ReferencedEntities, ExtractReferencedEntities(declaration));
            Absorb(controller.CustomizedEditors, ExtractCustomizedEditors(declaration));

            if (options.IncludeComments)
                Absorb(controller.SourceComments, ExtractComments(declaration));
        }

        return controller;
    }

    private static void Absorb(List<string> into, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!into.Contains(value, StringComparer.Ordinal))
                into.Add(value);
        }
    }

    /// <summary>
    /// One class declaration, and what can be told about it without leaving its file.
    /// </summary>
    private sealed record ClassCandidate(string FilePath, ClassDeclarationSyntax Declaration, bool ImportsXaf)
    {
        public string ClassName => Declaration.Identifier.Text;

        public string Namespace => GetNamespace(Declaration);

        public string FullName => Namespace.Length == 0 ? ClassName : $"{Namespace}.{ClassName}";

        public string BaseType => Declaration.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Extracts action declarations from controller field members and constructor logic.
    /// </summary>
    private static List<ExtractedAction> ExtractActions(ClassDeclarationSyntax classDecl, ExtractionOptions options)
    {
        var actions = new List<ExtractedAction>();

        // Find action field declarations: SimpleAction, PopupWindowShowAction, ParametrizedAction, etc.
        var actionFields = classDecl.Members.OfType<FieldDeclarationSyntax>()
            .Where(f => IsActionType(f.Declaration.Type.ToString()));

        foreach (var field in actionFields)
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var action = new ExtractedAction
                {
                    ActionId = variable.Identifier.Text,
                    ActionType = field.Declaration.Type.ToString(),
                };
                actions.Add(action);
            }
        }

        // Scan constructor for action creation and configuration
        var constructors = classDecl.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var ctor in constructors)
        {
            if (ctor.Body == null) continue;
            EnrichActionsFromConstructor(actions, ctor.Body, options);
        }

        return actions;
    }

    /// <summary>
    /// Enriches action metadata by scanning constructor assignments and event wiring.
    /// </summary>
    private static void EnrichActionsFromConstructor(List<ExtractedAction> actions, BlockSyntax body, ExtractionOptions options)
    {
        var classDecl = body.Ancestors().OfType<ClassDeclarationSyntax>().First();

        // Map field variable names to their action IDs for cross-referencing
        var fieldNameToAction = new Dictionary<string, ExtractedAction>();

        // Find new SimpleAction(...) / new PopupWindowShowAction(...) expressions
        var objectCreations = body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(o => IsActionType(o.Type.ToString()));

        foreach (var creation in objectCreations)
        {
            var typeName = creation.Type.ToString();

            // Try to find the variable it's assigned to
            var assignmentExpr = creation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault();
            var localDecl = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();

            string? fieldName = null;
            if (assignmentExpr != null)
                fieldName = assignmentExpr.Left.ToString();
            else if (localDecl != null)
                fieldName = localDecl.Identifier.Text;

            if (fieldName == null) continue;

            // Extract the real ActionId from constructor arguments
            string? actionId = fieldName;
            string? category = null;

            if (creation.ArgumentList != null)
            {
                var args = creation.ArgumentList.Arguments.ToList();
                if (args.Count >= 2)
                    actionId = SyntaxLiteral.ValueOf(args[1].Expression);
                if (args.Count >= 3)
                    category = SyntaxLiteral.ValueOf(args[2].Expression);
            }

            // Find or create the action entry
            var action = actions.FirstOrDefault(a => a.ActionId == fieldName);
            if (action == null)
            {
                action = new ExtractedAction { ActionType = typeName };
                actions.Add(action);
            }

            action.ActionId = actionId ?? fieldName;
            action.ActionType = typeName;
            if (category != null) action.Category = category;

            // Map field name to action for later event subscription lookup
            fieldNameToAction[fieldName] = action;

            // Extract properties from object initializer: new SimpleAction(...) { Caption = "...", ImageName = "..." }
            if (creation.Initializer != null)
            {
                foreach (var expr in creation.Initializer.Expressions)
                {
                    if (expr is AssignmentExpressionSyntax initAssignment)
                    {
                        var propName = initAssignment.Left.ToString();

                        if (ControllerTargetingReader.Apply(action.Targeting, propName, initAssignment.Right, classDecl))
                            continue;

                        var value = SyntaxLiteral.ValueOf(initAssignment.Right);

                        switch (propName)
                        {
                            case "Caption": action.Caption = value; break;
                            case "ConfirmationMessage": action.ConfirmationMessage = value; break;
                            case "ImageName": action.ImageName = value; break;
                            case "ToolTip": action.ToolTip = value; break;
                            case "Category": action.Category = value; break;
                            case "TargetObjectsCriteria": action.TargetObjectsCriteria = value; break;
                        }
                    }
                }
            }
        }

        // Find property assignments like: calcularComisionesAction.Caption = "..."
        var memberAccesses = body.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => a.Left is MemberAccessExpressionSyntax && !a.IsKind(SyntaxKind.AddAssignmentExpression));

        foreach (var assignment in memberAccesses)
        {
            if (assignment.Left is not MemberAccessExpressionSyntax memberAccess) continue;

            var objectName = memberAccess.Expression.ToString();
            var propertyName = memberAccess.Name.ToString();

            // Look up by field name or action ID
            var action = fieldNameToAction.GetValueOrDefault(objectName)
                         ?? actions.FirstOrDefault(a => a.ActionId == objectName || objectName.EndsWith(a.ActionId));

            if (action == null) continue;

            // An action's own view targeting, which narrows it further inside an already-active
            // controller. Read with the same rules, because XAF evaluates it with the same method.
            if (ControllerTargetingReader.Apply(action.Targeting, propertyName, assignment.Right, classDecl))
                continue;

            var value = SyntaxLiteral.ValueOf(assignment.Right);

            switch (propertyName)
            {
                case "Caption": action.Caption = value; break;
                case "ConfirmationMessage": action.ConfirmationMessage = value; break;
                case "ImageName": action.ImageName = value; break;
                case "ToolTip": action.ToolTip = value; break;
                case "Category": action.Category = value; break;
                case "TargetObjectsCriteria": action.TargetObjectsCriteria = value; break;
            }
        }

        // Find Execute event handler subscriptions: action.Execute += Method;
        var eventSubscriptions = body.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => a.IsKind(SyntaxKind.AddAssignmentExpression)
                        && a.Left is MemberAccessExpressionSyntax mae
                        && mae.Name.ToString() == "Execute");

        foreach (var sub in eventSubscriptions)
        {
            if (sub.Left is not MemberAccessExpressionSyntax mae) continue;

            var objectName = mae.Expression.ToString();

            // Look up by field name (calcularComisionesAction) or action ID
            var action = fieldNameToAction.GetValueOrDefault(objectName)
                         ?? actions.FirstOrDefault(a => a.ActionId == objectName || objectName.EndsWith(a.ActionId));

            if (action == null) continue;

            // Extract handler method name
            if (sub.Right is IdentifierNameSyntax handlerName)
            {
                action.ExecuteMethodName = handlerName.Identifier.Text;
            }
            else if (sub.Right is ObjectCreationExpressionSyntax handlerCreation)
            {
                if (handlerCreation.ArgumentList?.Arguments.Count > 0)
                    action.ExecuteMethodName = handlerCreation.ArgumentList.Arguments[0].Expression.ToString();
            }
        }

        // Now find the Execute handler method bodies
        if (options.IncludeMethodBodies)
        {
            foreach (var action in actions.Where(a => a.ExecuteMethodName != null))
            {
                var method = classDecl.Members.OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.Text == action.ExecuteMethodName);

                if (method?.Body != null)
                    action.ExecuteMethodBody = method.Body.ToString();
                else if (method?.ExpressionBody != null)
                    action.ExecuteMethodBody = method.ExpressionBody.ToString();
            }
        }
    }

    /// <summary>
    /// Extracts controller methods and optional XML summaries.
    /// </summary>
    private static List<ExtractedMethod> ExtractMethods(ClassDeclarationSyntax classDecl)
    {
        var methods = new List<ExtractedMethod>();

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            var extracted = new ExtractedMethod
            {
                Name = method.Identifier.Text,
                ReturnType = method.ReturnType.ToString(),
                IsPublic = method.Modifiers.Any(SyntaxKind.PublicKeyword),
                Parameters = method.ParameterList.Parameters
                    .Select(p => $"{p.Type} {p.Identifier.Text}")
                    .ToList(),
            };

            if (method.Body != null)
                extracted.Body = method.Body.ToString();
            else if (method.ExpressionBody != null)
                extracted.Body = method.ExpressionBody.ToString();

            // Extract XML doc comments
            var trivia = method.GetLeadingTrivia()
                .FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            if (trivia != default)
                extracted.Summary = trivia.ToString().Trim();

            methods.Add(extracted);
        }

        return methods;
    }

    /// <summary>
    /// Finds built-in editors this controller reconfigures at run time.
    /// </summary>
    /// <remarks>
    /// <c>View.CustomizeViewItemControl&lt;DateTimePropertyEditor&gt;(this, e =&gt; …)</c> is what
    /// DevExpress recommends for small changes to a built-in editor, and it leaves no trace
    /// anywhere else: no custom editor class, nothing on the entity, nothing in the Model Editor.
    /// A screen simply behaves differently from what its business class implies.
    /// </remarks>
    private static List<string> ExtractCustomizedEditors(ClassDeclarationSyntax classDecl)
    {
        var editors = new List<string>();

        var calls = classDecl.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(i => i.Expression is MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.Text: "CustomizeViewItemControl" }
            });

        foreach (var call in calls)
        {
            var generic = (GenericNameSyntax)((MemberAccessExpressionSyntax)call.Expression).Name;
            var editorType = generic.TypeArgumentList.Arguments.FirstOrDefault()?.ToString();

            if (!string.IsNullOrWhiteSpace(editorType))
                editors.Add(editorType);
        }

        return editors.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Collects referenced entity types from type checks and casts inside controller code.
    /// </summary>
    private static List<string> ExtractReferencedEntities(ClassDeclarationSyntax classDecl)
    {
        var entities = new HashSet<string>();
        var text = classDecl.ToString();

        // Find typeof(EntityName) references
        var typeofMatches = classDecl.DescendantNodes()
            .OfType<TypeOfExpressionSyntax>()
            .Select(t => t.Type.ToString())
            .Where(t => !t.StartsWith("System.") && !t.Contains("Controller") && !t.Contains("View"));

        foreach (var match in typeofMatches)
            entities.Add(match);

        // Find CurrentObject casts: ((PeriodoComision)View.CurrentObject)
        var casts = classDecl.DescendantNodes()
            .OfType<CastExpressionSyntax>()
            .Select(c => c.Type.ToString())
            .Where(t => !t.StartsWith("System.") && !t.Contains("Action") && !t.Contains("Event"));

        foreach (var cast in casts)
            entities.Add(cast);

        return entities.ToList();
    }

    #region Helper Methods

    /// <summary>
    /// Whether a class is an XAF controller, given what has been accepted so far.
    /// </summary>
    private static bool IsXafController(ClassCandidate candidate, HashSet<string> accepted, ExtractionOptions options)
    {
        if (candidate.Declaration.BaseList is null)
            return false;

        foreach (var baseType in candidate.Declaration.BaseList.Types)
        {
            var name = SimpleName(baseType.Type.ToString());

            if (name.Length == 0)
                continue;

            // The three entry points XAF documents.
            if (Array.Exists(options.ControllerBaseTypeNames, seed => name.Equals(seed, StringComparison.Ordinal)))
                return true;

            // A controller this project already accepted -- the team's own base class.
            if (accepted.Contains(name))
                return true;

            // A controller DevExpress ships, when this machine has a ground-truth catalog.
            if (options.Catalog?.FindController(name) is not null)
                return true;

            // Last resort, and the only rule that guesses: every XAF controller is named for what
            // it is, and the file has to import XAF for the name to mean this one. `ControllerBase`
            // is excluded because it belongs to ASP.NET and to nothing in XAF.
            if (candidate.ImportsXaf
                && name.EndsWith("Controller", StringComparison.Ordinal)
                && !name.Equals("ControllerBase", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Reduces a written type to its bare name: no namespace, no generic arguments.
    /// </summary>
    private static string SimpleName(string typeName)
    {
        var name = typeName.Trim();
        var generic = name.IndexOf('<');

        if (generic > 0)
            name = name[..generic];

        var lastDot = name.LastIndexOf('.');

        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }

    private static bool IsActionType(string typeName)
    {
        // HACK: The generic "contains Action" fallback may include non-action custom types.
        // It is preserved to maximize recall for heterogeneous legacy codebases.
        return typeName is "SimpleAction" or "PopupWindowShowAction" or "ParametrizedAction"
            or "SingleChoiceAction" or "PopupWindowShowAction"
            || typeName.Contains("Action") && !typeName.Contains("EventArgs");
    }

    private static string GetNamespace(ClassDeclarationSyntax classDecl)
    {
        var nsDecl = classDecl.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return nsDecl?.Name.ToString() ?? string.Empty;
    }

    private static string GetBaseTypeName(ClassDeclarationSyntax classDecl)
    {
        return classDecl.BaseList?.Types.FirstOrDefault()?.Type.ToString() ?? "object";
    }

    private static List<string> ExtractComments(ClassDeclarationSyntax classDecl)
    {
        var comments = new List<string>();

        var trivia = classDecl.GetLeadingTrivia()
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || t.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        || t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));

        foreach (var t in trivia)
            comments.Add(t.ToString().Trim());

        return comments;
    }

    #endregion
}
