namespace XafLogicExplainer.Core.Models;

/// <summary>
/// Represents seed data initialization logic extracted from updater methods.
/// </summary>
public class ExtractedSeedData
{
    /// <summary>
    /// Entity type created by the seed method.
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Method name that contains seed logic.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable inferred description of the seed operation.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Parsed records with assigned property values.
    /// </summary>
    public List<SeedRecord> Records { get; set; } = [];

    /// <summary>
    /// Raw source code snippet for traceability.
    /// </summary>
    public string RawSourceCode { get; set; } = string.Empty;
}

/// <summary>
/// Represents one seed record assignment set.
/// </summary>
public class SeedRecord
{
    /// <summary>
    /// Property/value assignments extracted from initializer or follow-up assignments.
    /// </summary>
    public Dictionary<string, string> PropertyValues { get; set; } = [];
}
