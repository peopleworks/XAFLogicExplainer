namespace XafLogicExplainer.Core.Models;

/// <summary>
/// The dialog XAF shows before a report opens: a <c>ReportParametersObjectBase</c> descendant.
/// </summary>
/// <remarks>
/// Every property is a field in the dialog, and <c>GetCriteria()</c> is what the fields mean —
/// <em>this report shows invoices dated in the period and, when a customer is chosen, only that
/// customer's</em>. The bodies are kept as source, the way seed methods are, because the
/// condition is the point and a paraphrase of a condition is where documentation goes wrong.
/// </remarks>
public class ReportParametersObject
{
    public string ClassName { get; set; } = string.Empty;

    /// <summary>File the class is declared in.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>1-based line of the class declaration.</summary>
    public int Line { get; set; }

    /// <summary>The dialog's fields: the class's public properties, in declaration order.</summary>
    public List<ReportParameterField> Fields { get; set; } = [];

    /// <summary>The <c>GetCriteria()</c> override, verbatim; null when the class has none.</summary>
    public string? CriteriaSource { get; set; }

    /// <summary>The <c>GetSorting()</c> override, verbatim; null when the class has none.</summary>
    public string? SortingSource { get; set; }
}

/// <summary>One field of the parameters dialog.</summary>
public class ReportParameterField
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The type as written — an entity name means a lookup in the dialog.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The initializer as written, when there is one: what the dialog opens with.</summary>
    public string? Default { get; set; }
}
