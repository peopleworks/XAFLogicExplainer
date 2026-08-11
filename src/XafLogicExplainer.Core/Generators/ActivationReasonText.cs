using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Generators;

/// <summary>
/// Puts an activation reason into words.
/// </summary>
/// <remarks>
/// One extraction feeds an English page, an English tool response and a Spanish document, so the
/// resolver records which condition held rather than a sentence saying so. This is the only place
/// that turns it into prose.
/// </remarks>
public static class ActivationReasonText
{
    /// <summary>Phrases a reason in English.</summary>
    /// <param name="reason">The condition that held.</param>
    public static string English(ActivationReason reason) => reason.Condition switch
    {
        ActivationCondition.Nesting =>
            $"the view is {(reason.Required == "Root" ? "root" : "nested")}",

        ActivationCondition.ViewType when reason.Actual is null =>
            $"view is a {reason.Required}",
        ActivationCondition.ViewType when reason.Required == "ObjectView" =>
            $"a {reason.Actual} is an ObjectView",
        ActivationCondition.ViewType =>
            $"it targets {reason.Required}, a view class this analysis cannot place",

        ActivationCondition.ObjectType when reason.Actual is null =>
            $"shows {reason.Required}",
        ActivationCondition.ObjectType =>
            $"{reason.Actual} is a {reason.Required}",

        _ => "its id is named in TargetViewId",
    };

    /// <summary>Phrases a reason in Spanish.</summary>
    /// <param name="reason">The condition that held.</param>
    public static string Spanish(ActivationReason reason) => reason.Condition switch
    {
        ActivationCondition.Nesting =>
            $"la vista es {(reason.Required == "Root" ? "raiz" : "anidada")}",

        ActivationCondition.ViewType when reason.Actual is null =>
            $"la vista es un {reason.Required}",
        ActivationCondition.ViewType when reason.Required == "ObjectView" =>
            $"un {reason.Actual} es un ObjectView",
        ActivationCondition.ViewType =>
            $"apunta a {reason.Required}, una clase de vista que este analisis no puede situar",

        ActivationCondition.ObjectType when reason.Actual is null =>
            $"muestra {reason.Required}",
        ActivationCondition.ObjectType =>
            $"{reason.Actual} es un {reason.Required}",

        _ => "su id esta nombrado en TargetViewId",
    };

    /// <summary>Phrases a reason in the requested language.</summary>
    /// <param name="reason">The condition that held.</param>
    /// <param name="spanish">Whether to write Spanish.</param>
    public static string ForLanguage(ActivationReason reason, bool spanish) =>
        spanish ? Spanish(reason) : English(reason);
}
