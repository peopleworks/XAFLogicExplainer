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
    /// Target property name when rule is property-scoped.
    /// </summary>
    public string? TargetProperty { get; set; }

    /// <summary>
    /// User-facing validation message template when available.
    /// </summary>
    public string? MessageTemplate { get; set; }

    /// <summary>
    /// Rule criteria/context expression when present.
    /// </summary>
    public string? TargetCriteria { get; set; }

    /// <summary>
    /// Additional raw rule arguments captured as key/value pairs.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = [];
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
