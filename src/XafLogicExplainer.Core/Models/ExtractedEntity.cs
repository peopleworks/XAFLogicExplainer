namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Describes one extracted business entity and its structural metadata.
/// </summary>
public class ExtractedEntity
{
    /// <summary>
    /// Entity class name.
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// Declared namespace.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// Source file path for the entity declaration.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the project this entity was extracted from (e.g. "Module", "Blazor.Server").
    /// </summary>
    public string? SourceProject { get; set; }

    /// <summary>
    /// Description text read from attributes when available.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Navigation group name associated with this entity.
    /// </summary>
    public string? NavigationGroup { get; set; }

    /// <summary>
    /// Display/default property name used by XAF.
    /// </summary>
    public string? DefaultProperty { get; set; }

    /// <summary>
    /// Base type string from inheritance chain.
    /// </summary>
    public string BaseType { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether <c>DefaultClassOptions</c> is present.
    /// </summary>
    public bool IsDefaultClassOptions { get; set; }

    /// <summary>
    /// Indicates whether this entity is persistent.
    /// </summary>
    public bool IsPersistent { get; set; } = true;

    /// <summary>
    /// Scalar and collection properties discovered in source.
    /// </summary>
    public List<ExtractedProperty> Properties { get; set; } = [];

    /// <summary>
    /// Relationships inferred from attributes and navigation properties.
    /// </summary>
    public List<ExtractedRelationship> Relationships { get; set; } = [];

    /// <summary>
    /// Validation rules declared at class or property level.
    /// </summary>
    public List<ExtractedValidationRule> ValidationRules { get; set; } = [];

    /// <summary>
    /// Appearance/customization rules declared via attributes.
    /// </summary>
    public List<ExtractedAppearanceRule> AppearanceRules { get; set; } = [];

    /// <summary>
    /// Additional inferred business logic rules.
    /// </summary>
    public List<ExtractedBusinessRule> InferredBusinessRules { get; set; } = [];

    /// <summary>
    /// Source comments found around the class declaration.
    /// </summary>
    public List<string> SourceComments { get; set; } = [];

    /// <summary>
    /// Caption value extracted from Model Editor metadata.
    /// </summary>
    public string? ModelCaption { get; set; }

    /// <summary>
    /// Indicates whether class is marked cloneable in model metadata.
    /// </summary>
    public bool IsCloneable { get; set; }
}

/// <summary>
/// Describes one extracted entity property.
/// </summary>
public class ExtractedProperty
{
    /// <summary>
    /// Property name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Declared CLR type text.
    /// </summary>
    public string TypeName { get; set; } = string.Empty;

    /// <summary>
    /// User-facing description from attributes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Size constraint when defined.
    /// </summary>
    public int? Size { get; set; }

    /// <summary>
    /// Indicates whether property is part of the key.
    /// </summary>
    public bool IsKey { get; set; }

    /// <summary>
    /// Indicates whether property represents a collection relation.
    /// </summary>
    public bool IsCollection { get; set; }

    /// <summary>
    /// Indicates whether property is computed or non-persistent.
    /// </summary>
    public bool IsComputed { get; set; }

    /// <summary>
    /// Indicates whether value is required by rules or attributes.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Display name override.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Tooltip metadata.
    /// </summary>
    public string? ToolTip { get; set; }

    /// <summary>
    /// Display format metadata.
    /// </summary>
    public string? DisplayFormat { get; set; }

    /// <summary>
    /// Editor alias metadata.
    /// </summary>
    public string? EditorAlias { get; set; }

    /// <summary>
    /// Indicates list view visibility.
    /// </summary>
    public bool VisibleInListView { get; set; } = true;

    /// <summary>
    /// Indicates detail view visibility.
    /// </summary>
    public bool VisibleInDetailView { get; set; } = true;

    /// <summary>
    /// Persistent alias expression (XPO).
    /// </summary>
    public string? PersistentAlias { get; set; }

    /// <summary>
    /// Data source criteria expression.
    /// </summary>
    public string? DataSourceCriteria { get; set; }

    /// <summary>
    /// Indicates whether ImmediatePostData is enabled.
    /// </summary>
    public bool ImmediatePostData { get; set; }

    /// <summary>
    /// Default value metadata from model defaults or initializer.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Attribute expressions not mapped to common fields.
    /// </summary>
    public List<string> CustomAttributes { get; set; } = [];
}

/// <summary>
/// Describes a relationship between entities inferred from one property.
/// </summary>
public class ExtractedRelationship
{
    /// <summary>
    /// Relationship source property name.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Related target entity type.
    /// </summary>
    public string RelatedEntity { get; set; } = string.Empty;

    /// <summary>
    /// XAF association identifier when declared.
    /// </summary>
    public string? AssociationName { get; set; }

    /// <summary>
    /// Relationship direction/cardinality category.
    /// </summary>
    public RelationshipType Type { get; set; }

    /// <summary>
    /// Indicates aggregate composition semantics.
    /// </summary>
    public bool IsAggregated { get; set; }
}

/// <summary>
/// Relationship cardinality model.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// Many source items can reference one target item.
    /// </summary>
    ManyToOne,

    /// <summary>
    /// One source item can reference many target items.
    /// </summary>
    OneToMany,

    /// <summary>
    /// Many source items can reference many target items.
    /// </summary>
    ManyToMany
}
