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
        var controllers = new List<ExtractedController>();
        var controllerDir = Path.Combine(sourceDirectory, "Controllers");

        if (!Directory.Exists(controllerDir))
        {
            // Fallback: search entire project for *Controller.cs files
            controllerDir = sourceDirectory;
        }

        var csFiles = Directory.GetFiles(controllerDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => BuildOutputFilter.IsAnalyzable(f, controllerDir));

        foreach (var file in csFiles)
        {
            var controller = AnalyzeControllerFile(file, options);
            if (controller != null)
                controllers.Add(controller);
        }

        return controllers;
    }

    /// <summary>
    /// Analyzes one C# file and extracts controller information when applicable.
    /// </summary>
    public ExtractedController? AnalyzeControllerFile(string filePath, ExtractionOptions options)
    {
        var source = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root = tree.GetRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => IsXafController(c, options.ControllerBaseTypeNames));

        if (classDecl == null) return null;

        var controller = new ExtractedController
        {
            ClassName = classDecl.Identifier.Text,
            Namespace = GetNamespace(classDecl),
            FilePath = filePath,
            BaseControllerType = GetBaseTypeName(classDecl),
        };

        // Where this controller activates: object type, view type, nesting and view id, read the
        // way XAF itself evaluates them.
        controller.Targeting = ControllerTargetingReader.Read(classDecl);

        // Extract actions from fields and constructor
        controller.Actions.AddRange(ExtractActions(classDecl, options));

        // Extract methods
        if (options.IncludeMethodBodies)
        {
            controller.Methods.AddRange(ExtractMethods(classDecl));
        }

        // Extract referenced entities
        controller.ReferencedEntities.AddRange(ExtractReferencedEntities(classDecl));
        controller.CustomizedEditors.AddRange(ExtractCustomizedEditors(classDecl));

        // Extract comments
        if (options.IncludeComments)
        {
            controller.SourceComments.AddRange(ExtractComments(classDecl));
        }

        return controller;
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
                        var value = SyntaxLiteral.ValueOf(initAssignment.Right);

                        switch (propName)
                        {
                            case "Caption": action.Caption = value; break;
                            case "ConfirmationMessage": action.ConfirmationMessage = value; break;
                            case "ImageName": action.ImageName = value; break;
                            case "ToolTip": action.ToolTip = value; break;
                            case "Category": action.Category = value; break;
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
            var value = SyntaxLiteral.ValueOf(assignment.Right);

            // Look up by field name or action ID
            var action = fieldNameToAction.GetValueOrDefault(objectName)
                         ?? actions.FirstOrDefault(a => a.ActionId == objectName || objectName.EndsWith(a.ActionId));

            if (action == null) continue;

            switch (propertyName)
            {
                case "Caption": action.Caption = value; break;
                case "ConfirmationMessage": action.ConfirmationMessage = value; break;
                case "ImageName": action.ImageName = value; break;
                case "ToolTip": action.ToolTip = value; break;
                case "Category": action.Category = value; break;
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
            var classDecl = body.Ancestors().OfType<ClassDeclarationSyntax>().First();
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

    private static bool IsXafController(ClassDeclarationSyntax classDecl, string[] controllerBaseTypeNames)
    {
        if (classDecl.BaseList == null) return false;

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            var simpleTypeName = typeName.Contains('<') ? typeName[..typeName.IndexOf('<')] : typeName;

            if (controllerBaseTypeNames.Any(ct => simpleTypeName.Equals(ct, StringComparison.Ordinal)
                                                   || simpleTypeName.EndsWith($".{ct}")))
                return true;
        }

        return false;
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
