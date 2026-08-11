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

        // What the base could not read, the derived class inherits -- unless it set that same
        // condition itself, in which case the base's value never mattered.
        foreach (var note in inherited.Unreadable)
        {
            if (!derived.Declared.Contains(ConditionOf(note)))
                derived.Unreadable.Add(note);
        }

        if (derived.Declared.Contains(ViewIdCondition))
            return;

        derived.ViewIds = [.. inherited.ViewIds];
        derived.UnresolvedViewId = inherited.UnresolvedViewId;
    }

    /// <summary>
    /// Which condition an unreadable note is about.
    /// </summary>
    private static string ConditionOf(string note) => note.Split(' ')[0] switch
    {
        "TargetObjectType" => nameof(ControllerTargeting.TargetObjectType),
        "TargetViewNesting" => nameof(ControllerTargeting.Nesting),
        _ => nameof(ControllerTargeting.TypeOfView),
    };

    /// <summary>
    /// Folds one declaration of a partial class into the targeting accumulated for the whole class.
    /// </summary>
    /// <remarks>
    /// The opposite direction to <see cref="Inherit"/>: there the derived class wins and the base
    /// only fills gaps, here every part is the same class, so whatever a part states is stated by
    /// the class. It matters because the XAF designer splits a controller across two files and puts
    /// the targeting in the generated half.
    /// </remarks>
    /// <param name="into">Targeting for the whole class, updated in place.</param>
    /// <param name="part">Targeting read from one declaration.</param>
    public static void Merge(ControllerTargeting into, ControllerTargeting part)
    {
        if (part.Declared.Contains(nameof(ControllerTargeting.TargetObjectType)))
            into.TargetObjectType = part.TargetObjectType;

        if (part.Declared.Contains(nameof(ControllerTargeting.TypeOfView)))
            into.TypeOfView = part.TypeOfView;

        if (part.Declared.Contains(nameof(ControllerTargeting.Nesting)))
            into.Nesting = part.Nesting;

        if (part.Declared.Contains(ViewIdCondition))
        {
            into.ViewIds = [.. part.ViewIds];
            into.UnresolvedViewId = part.UnresolvedViewId;
        }

        foreach (var condition in part.Declared)
            into.Declared.Add(condition);

        foreach (var note in part.Unreadable)
            into.Unreadable.Add(note);
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
        foreach (var body in SetUpBodies(classDecl))
        {
            foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                // `new SimpleAction(this, …) { TargetViewId = … }` reads as a bare identifier on
                // the left, exactly like the controller's own property. It belongs to the action.
                if (assignment.Ancestors().OfType<InitializerExpressionSyntax>().Any())
                    continue;

                if (PropertyAssignedOnThis(assignment.Left) is { } property)
                    Apply(targeting, property, assignment.Right, classDecl);
            }
        }
    }

    /// <summary>
    /// Applies one <c>Target…</c> assignment, whoever it was written on.
    /// </summary>
    /// <remarks>
    /// Public because an action carries its own copy of the same four properties. XAF evaluates
    /// them with the very same <c>IsFitToView</c>, to decide whether an action appears inside an
    /// already-active controller, so they are read with the very same rules.
    /// </remarks>
    /// <param name="targeting">Targeting to update.</param>
    /// <param name="property">Property being assigned.</param>
    /// <param name="value">Expression assigned to it.</param>
    /// <param name="classDecl">Declaring class, used to resolve constants.</param>
    /// <returns>Whether the property was one of the four conditions.</returns>
    public static bool Apply(
        ControllerTargeting targeting,
        string property,
        ExpressionSyntax value,
        ClassDeclarationSyntax classDecl)
    {
        var parameters = TypeParameters(classDecl);

        switch (property)
        {
            // ViewController<T>'s own constructor assigns typeof(T). That restricts whatever closes
            // the generic, and nothing at all by itself.
            case "TargetObjectType" when value is TypeOfExpressionSyntax objectType:
                if (parameters.Contains(objectType.Type.ToString()))
                    return true;

                targeting.TargetObjectType = LastSegment(objectType.Type.ToString());
                targeting.Declared.Add(nameof(ControllerTargeting.TargetObjectType));
                return true;

            case "TypeOfView" when value is TypeOfExpressionSyntax viewType:
                if (parameters.Contains(viewType.Type.ToString()))
                    return true;

                targeting.TypeOfView = NormalizeViewType(viewType.Type.ToString());
                targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                return true;

            case "TargetViewType" when EnumMember(value, ViewTypeMembers) is { } viewTypeMember:
                targeting.TypeOfView = NormalizeViewType(viewTypeMember);
                targeting.Declared.Add(nameof(ControllerTargeting.TypeOfView));
                return true;

            case "TargetViewNesting" when EnumMember(value, NestingMembers) is { } nestingMember:
                targeting.Nesting = NormalizeNesting(nestingMember);
                targeting.Declared.Add(nameof(ControllerTargeting.Nesting));
                return true;

            case "TargetViewId":
                ReadViewId(classDecl, value, targeting);
                targeting.Declared.Add(ViewIdCondition);
                return true;

            // The property is one of the four and the value is not something this analysis can
            // read: a ternary, a variable, a call. Recording it is the whole point -- dropping it
            // leaves the condition looking unset, which reads as "no restriction".
            case "TargetObjectType" or "TypeOfView" or "TargetViewType" or "TargetViewNesting":
                targeting.Unreadable.Add($"{property} = {value}");
                return true;

            default:
                return false;
        }
    }

    private static readonly string[] ViewTypeMembers = ["Any", "ListView", "DetailView", "DashboardView"];

    private static readonly string[] NestingMembers = ["Any", "Root", "Nested"];

    /// <summary>
    /// The enum member an expression names, when it plainly names one.
    /// </summary>
    /// <remarks>
    /// Only a bare name or a dotted one. Taking the text after the last dot of <em>any</em>
    /// expression turned <c>isList ? ViewType.ListView : ViewType.DetailView</c> into a confident
    /// restriction to <c>DetailView</c>.
    /// </remarks>
    private static string? EnumMember(ExpressionSyntax value, string[] members)
    {
        if (value is not (IdentifierNameSyntax or MemberAccessExpressionSyntax))
            return null;

        var name = LastSegment(value.ToString());

        return Array.Exists(members, member => member == name) ? name : null;
    }

    /// <summary>
    /// Every place a controller sets itself up before it is ever activated.
    /// </summary>
    /// <remarks>
    /// Constructors, including expression-bodied ones, and <c>InitializeComponent</c> — which is
    /// where the XAF designer puts the targeting, in the generated half of a partial class. Reading
    /// constructor blocks alone made every designer-era controller, the standard shape of migrated
    /// applications, come out as "restricts nothing, runs on every screen".
    /// </remarks>
    private static IEnumerable<SyntaxNode> SetUpBodies(ClassDeclarationSyntax classDecl)
    {
        foreach (var ctor in classDecl.Members.OfType<ConstructorDeclarationSyntax>())
        {
            if (ctor.Body is { } block)
                yield return block;
            else if (ctor.ExpressionBody is { } expression)
                yield return expression;
        }

        foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Identifier.Text != "InitializeComponent")
                continue;

            if (method.Body is { } block)
                yield return block;
            else if (method.ExpressionBody is { } expression)
                yield return expression;
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
        MemberAccessExpressionSyntax
        {
            // `base.TargetViewType = …` sets the same property as `this.TargetViewType = …`.
            Expression: ThisExpressionSyntax or BaseExpressionSyntax,
        } member => member.Name.Identifier.Text,
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

        // Exactly as XAF splits it: the whole string is compared first, then each part, and it does
        // NOT trim. `TargetViewId = "Order_ListView; Customer_ListView"` therefore never activates
        // on Customer_ListView, because the entry is " Customer_ListView" with the space. Trimming
        // here reported a controller on a screen it silently never reaches -- and printing the
        // untrimmed id is what makes that visible to whoever wrote it.
        targeting.ViewIds.AddRange(text.Contains(';') ? [text, .. text.Split(';')] : [text]);
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
