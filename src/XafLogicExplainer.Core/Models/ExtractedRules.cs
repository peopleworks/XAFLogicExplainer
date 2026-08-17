namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Represents one validation rule declared on an entity or property.
/// </summary>
public class ExtractedValidationRule
{
    /// <summary>
    /// Rule attribute type (for example RuleRequiredField).
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// The rule's identifier, when the attribute was given one.
    /// </summary>
    /// <remarks>
    /// It is how a rule is referred to everywhere outside the source — in the Model Editor, in a
    /// validation error a user reports, in the sentence a developer writes about it. It was read,
    /// but only into <see cref="Parameters"/> under the key <c>arg0</c>, which is where the
    /// renderers found it and printed it that way.
    /// </remarks>
    public string? Id { get; set; }

    /// <summary>
    /// The validation contexts the rule belongs to, as written.
    /// </summary>
    /// <remarks>
    /// Almost always <c>DefaultContexts.Save</c>. The interesting case is the other one: a rule
    /// declared in a context of the application's own fires only where that context is validated,
    /// so a reader who assumes every rule runs on save is wrong about it in the direction that
    /// matters — believing something is enforced when it is not.
    /// </remarks>
    public string? Contexts { get; set; }

    /// <summary>
    /// Target property name when rule is property-scoped.
    /// </summary>
    public string? TargetProperty { get; set; }

    /// <summary>
    /// User-facing validation message template when available.
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    /// The criteria the rule enforces, for <c>RuleCriteria</c>.
    /// </summary>
    /// <remarks>
    /// Not the same thing as <see cref="TargetCriteria"/>, and conflating them loses the rule
    /// itself. <c>[RuleCriteria("Prescription_NotExpired", DefaultContexts.Save,
    /// "ExpiresOn &gt; IssuedOn")]</c> says what must be true; a target criteria says when the rule
    /// applies at all. Only the second was being read, so the expression a user actually hits was
    /// missing from an index that claimed to hold every one.
    /// </remarks>
    public string? Expression { get; set; }

    /// <summary>
    /// Rule criteria/context expression when present.
    /// </summary>
    public string? TargetCriteria { get; set; }

    /// <summary>
    /// Additional raw rule arguments captured as key/value pairs.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = [];

    /// <summary>
    /// The class that declared this rule, when it is not the entity listing it.
    /// </summary>
    /// <remarks>
    /// <c>null</c> for a rule the entity declares itself. A rule declared on a shared base is
    /// enforced when any descendant is saved, so it belongs under each of them; where it was
    /// written is what tells a reader that changing it changes the whole application.
    /// </remarks>
    public string? InheritedFrom { get; set; }

    /// <summary>A copy this rule's declarer does not share.</summary>
    public ExtractedValidationRule Clone()
    {
        var copy = (ExtractedValidationRule)MemberwiseClone();
        copy.Parameters = new Dictionary<string, string>(Parameters);
        return copy;
    }
}

/// <summary>
/// Represents one appearance customization rule declared on an entity.
/// </summary>
public class ExtractedAppearanceRule
{
    /// <summary>
    /// Rule identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Target items expression.
    /// </summary>
    public string? TargetItems { get; set; }

    /// <summary>
    /// Criteria expression controlling when the rule is active.
    /// </summary>
    public string? Criteria { get; set; }

    /// <summary>
    /// UI context where rule applies.
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Visibility behavior value.
    /// </summary>
    public string? Visibility { get; set; }

    /// <summary>
    /// Enablement behavior value.
    /// </summary>
    public string? Enabled { get; set; }

    /// <summary>
    /// Background color override value.
    /// </summary>
    public string? BackColor { get; set; }

    /// <summary>
    /// Font color override value.
    /// </summary>
    public string? FontColor { get; set; }

    /// <summary>
    /// The class that declared this rule, when it is not the entity listing it.
    /// </summary>
    /// <remarks>
    /// <c>null</c> for a rule the entity declares itself. An appearance rule on a base greys the
    /// same field on every screen below it, which is a thing a reader of one screen has no other
    /// way to learn.
    /// </remarks>
    public string? InheritedFrom { get; set; }

    /// <summary>A copy this rule's declarer does not share.</summary>
    public ExtractedAppearanceRule Clone() => (ExtractedAppearanceRule)MemberwiseClone();
}

/// <summary>
/// Represents an inferred business rule synthesized from analyzed artifacts.
/// </summary>
public class ExtractedBusinessRule
{
    /// <summary>
    /// Human-readable rule text.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Rule category classification.
    /// </summary>
    public BusinessRuleCategory Category { get; set; }

    /// <summary>
    /// Source reference where the rule was inferred from.
    /// </summary>
    public string? SourceLocation { get; set; }

    /// <summary>
    /// Related entity name when applicable.
    /// </summary>
    public string? RelatedEntity { get; set; }

    /// <summary>
    /// Optional code excerpt associated with the inferred rule.
    /// </summary>
    public string? CodeSnippet { get; set; }
}

/// <summary>
/// Taxonomy for inferred business rule grouping.
/// </summary>
public enum BusinessRuleCategory
{
    /// <summary>
    /// Numeric or expression-based calculations.
    /// </summary>
    Calculation,

    /// <summary>
    /// Data validation and consistency checks.
    /// </summary>
    Validation,

    /// <summary>
    /// Explicit exclusion logic.
    /// </summary>
    Exclusion,

    /// <summary>
    /// Module or runtime configuration rules.
    /// </summary>
    Configuration,

    /// <summary>
    /// Workflow/state transition logic.
    /// </summary>
    Workflow,

    /// <summary>
    /// Mapping, normalization, and transformation logic.
    /// </summary>
    DataTransformation,

    /// <summary>
    /// Access and authorization constraints.
    /// </summary>
    AccessControl
}
