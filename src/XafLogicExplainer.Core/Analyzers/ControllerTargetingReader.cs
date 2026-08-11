using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Reads the four conditions that decide where a controller activates.
/// </summary>
/// <remarks>
/// Shared deliberately. An application's controllers and the ones DevExpress ships declare their
/// targeting in exactly the same C#, so the same reader serves the extractor and the ground-truth
/// catalog. Framework controllers overwhelmingly assign theirs inside a constructor, which is why
/// reflection over the assemblies cannot see it and the catalog needs the installed sources.
/// </remarks>
public static class ControllerTargetingReader
{
    /// <summary>
    /// Reads a controller's targeting from its declaration.
    /// </summary>
    /// <param name="classDecl">The controller class.</param>
    public static ControllerTargeting Read(ClassDeclarationSyntax classDecl)
    {
        var targeting = new ControllerTargeting();

        ReadFromBaseType(classDecl, targeting);

        // Constructors run after the base type has had its say, and an explicit assignment states
        // the intent outright, so it wins.
        ReadFromConstructors(classDecl, targeting);

        return targeting;
    }

    /// <summary>
    /// Normalizes any spelling of a view type to the view class name, or null when unrestricted.
    /// </summary>
    /// <param name="text">
    /// <c>ViewType.ListView</c>, <c>typeof(DetailView)</c>'s argument, or a bare class name.
    /// </param>
    public static string? NormalizeViewType(string? text)
    {
        var name = LastSegment(text);

        // typeof(View) and ViewType.Any are how a controller says "no restriction". Anything else,
        // including a view class the framework does not define, is a real restriction and is kept
        // verbatim rather than flattened to "any view".
        return name is null or "" or "View" or "Any" ? null : name;
    }

    /// <summary>
    /// Normalizes a nesting expression to <c>Root</c>, <c>Nested</c>, or null for either.
    /// </summary>
    /// <param name="text">A <c>Nesting</c> enum member, qualified or not.</param>
    public static string? NormalizeNesting(string? text) =>
        LastSegment(text) is "Root" or "Nested" ? LastSegment(text) : null;

    /// <summary>
    /// The condition covered by <c>TargetViewId</c>, which sets two fields at once.
    /// </summary>
    private const string ViewIdCondition = "ViewId";

    /// <summary>
    /// Layers a controller's own targeting over what it inherits from its base controller.
    /// </summary>
    /// <remarks>
    /// Targeting is inherited, because it is set by a constructor and base constructors run first.
    /// Reading a class in isolation therefore reports every controller whose base does the
    /// targeting as unrestricted — in the DevExpress 26.1 framework that is 33 controllers claimed
    /// to run on every view in the application when they run on one kind of view.
    /// </remarks>
    /// <param name="derived">The controller's own targeting, completed in place.</param>
    /// <param name="inherited">Effective targeting of the base controller.</param>
    public static void Inherit(ControllerTargeting derived, ControllerTargeting inherited)
    {
        if (!derived.Declared.Contains(nameof(ControllerTargeting.TargetObjectType)))
            derived.TargetObjectType = inherited.TargetObjectType;

        if (!derived.Declared.Contains(nameof(ControllerTargeting.TypeOfView)))
            derived.TypeOfView = inherited.TypeOfView;

        if (!derived.Declared.Contains(nameof(ControllerTargeting.Nesting)))
            derived.Nesting = inherited.Nesting;

        if (derived.Declared.Contains(ViewIdCondition))
            return;

        derived.ViewIds = [.. inherited.ViewIds];
        derived.UnresolvedViewId = inherited.UnresolvedViewId;
    }

    /// <summary>
    /// Takes targeting from the generic base type.
    /// </summary>
    private static void ReadFromBaseType(ClassDeclarationSyntax classDecl, ControllerTargeting targeting)
    {
        if (Unqualify(classDecl.BaseList?.Types.FirstOrDefault()?.Type) is not GenericNameSyntax generic)
            return;

        var arguments = generic.TypeArgumentList.Arguments;

        // class Reusable<T> : ViewController<T> constrains nothing in particular; only the classes
        // that close the generic do.
        var parameters = TypeParameters(classDecl);

        if (arguments.Any(argument => parameters.Contains(argument.ToString())))
            return;

        switch (generic.Identifier.Text)
        {
            // ObjectViewController<DetailView, Order>: the view first, the business class second.
            case "ObjectViewController" when arguments.Count == 2:
                targeting.TypeOfView = NormalizeViewType(arguments[0].ToString());
                targeting.TargetObjectType = LastSegment(arguments[1].ToString());
                targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                targeting.Declared.Add(nameof(ControllerTargeting.TargetObjectType));
                break;

            // ViewController<DetailView> constrains the *view*, not the object -- the single
            // argument is a View subclass. Reading it as a business type reported "targets
            // DetailView" for a controller that targets Order.
            case "ViewController" when arguments.Count == 1:
                targeting.TypeOfView = NormalizeViewType(arguments[0].ToString());
                targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                break;
        }
    }

    /// <summary>
    /// Names of the class's own generic parameters.
    /// </summary>
    private static HashSet<string> TypeParameters(ClassDeclarationSyntax classDecl) =>
        classDecl.TypeParameterList?.Parameters
            .Select(parameter => parameter.Identifier.Text)
            .ToHashSet(StringComparer.Ordinal) ?? [];

    /// <summary>
    /// Takes targeting from assignments in the controller's constructors.
    /// </summary>
    private static void ReadFromConstructors(ClassDeclarationSyntax classDecl, ControllerTargeting targeting)
    {
        foreach (var ctor in classDecl.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Body is null)
                continue;

            var parameters = TypeParameters(classDecl);

            foreach (var assignment in ctor.Body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (PropertyAssignedOnThis(assignment.Left) is not { } property)
                    continue;

                switch (property)
                {
                    // ViewController<T>'s own constructor assigns typeof(T). It restricts whatever
                    // closes the generic, and nothing at all by itself.
                    case "TargetObjectType" when assignment.Right is TypeOfExpressionSyntax objectType
                                                 && !parameters.Contains(objectType.Type.ToString()):
                        targeting.TargetObjectType = LastSegment(objectType.Type.ToString());
                        targeting.Declared.Add(nameof(ControllerTargeting.TargetObjectType));
                        break;

                    case "TypeOfView" when assignment.Right is TypeOfExpressionSyntax viewType
                                           && !parameters.Contains(viewType.Type.ToString()):
                        targeting.TypeOfView = NormalizeViewType(viewType.Type.ToString());
                        targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                        break;

                    case "TargetViewType":
                        targeting.TypeOfView = NormalizeViewType(assignment.Right.ToString());
                        targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                        break;

                    case "TargetViewNesting":
                        targeting.Nesting = NormalizeNesting(assignment.Right.ToString());
                        targeting.Declared.Add(nameof(ControllerTargeting.Nesting));
                        break;

                    case "TargetViewId":
                        ReadViewId(classDecl, assignment.Right, targeting);
                        targeting.Declared.Add(ViewIdCondition);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// The property name when an assignment targets the controller itself, otherwise null.
    /// </summary>
    /// <remarks>
    /// <c>mergeAction.TargetViewId = "..."</c> looks identical but sets an <em>action's</em> own
    /// targeting, which XAF evaluates separately and which narrows the action further within an
    /// already-active controller. Reading it here would attribute the action's restriction to the
    /// whole controller.
    /// </remarks>
    private static string? PropertyAssignedOnThis(ExpressionSyntax left) => left switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.Text,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member => member.Name.Identifier.Text,
        _ => null,
    };

    /// <summary>
    /// Resolves an assignment to <c>TargetViewId</c> into the identifiers it restricts to.
    /// </summary>
    private static void ReadViewId(
        ClassDeclarationSyntax classDecl,
        ExpressionSyntax expression,
        ControllerTargeting targeting)
    {
        targeting.ViewIds.Clear();
        targeting.UnresolvedViewId = null;

        if (ResolveString(classDecl, expression) is not { } text)
        {
            targeting.UnresolvedViewId = expression.ToString();
            return;
        }

        // An empty string and "Any" -- the value of ActionBase.AnyCaption -- both mean no
        // restriction at all.
        if (text.Length == 0 || text == "Any")
            return;

        targeting.ViewIds.AddRange(
            text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Reads the string an expression stands for without a compilation, or null when it cannot.
    /// </summary>
    private static string? ResolveString(ClassDeclarationSyntax classDecl, ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;

            // string.Empty
            case MemberAccessExpressionSyntax { Expression: PredefinedTypeSyntax, Name.Identifier.Text: "Empty" }:
                return string.Empty;

            // ActionBase.AnyCaption, however it is qualified.
            case MemberAccessExpressionSyntax { Name.Identifier.Text: "AnyCaption" }:
            case IdentifierNameSyntax { Identifier.Text: "AnyCaption" }:
                return "Any";

            // A const on the controller itself. DevExpress writes its own dialog controllers this
            // way, and so does any application that shares an id between a controller and a view.
            case IdentifierNameSyntax identifier:
                return ConstantValue(classDecl, identifier.Identifier.Text);

            case MemberAccessExpressionSyntax member
                when member.Expression.ToString() == classDecl.Identifier.Text:
                return ConstantValue(classDecl, member.Name.Identifier.Text);

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
            {
                var left = ResolveString(classDecl, binary.Left);
                var right = ResolveString(classDecl, binary.Right);

                return left is null || right is null ? null : left + right;
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// The value of a string constant declared on this class, or null.
    /// </summary>
    private static string? ConstantValue(ClassDeclarationSyntax classDecl, string name)
    {
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!field.Modifiers.Any(SyntaxKind.ConstKeyword))
                continue;

            foreach (var variable in field.Declaration.Variables)
            {
                if (variable.Identifier.Text != name)
                    continue;

                return variable.Initializer?.Value is LiteralExpressionSyntax literal
                       && literal.IsKind(SyntaxKind.StringLiteralExpression)
                    ? literal.Token.ValueText
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Drops any namespace or enum qualifier: <c>DevExpress.ExpressApp.ViewType.ListView</c> reads
    /// the same as <c>ViewType.ListView</c> and as <c>ListView</c>.
    /// </summary>
    private static string? LastSegment(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var name = text.Trim();
        var lastDot = name.LastIndexOf('.');

        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }

    /// <summary>
    /// Reduces <c>Namespace.ViewController&lt;T&gt;</c> to the generic name Roslyn keeps nested
    /// inside a qualified one.
    /// </summary>
    private static TypeSyntax? Unqualify(TypeSyntax? type) =>
        type is QualifiedNameSyntax qualified ? qualified.Right : type;
}
