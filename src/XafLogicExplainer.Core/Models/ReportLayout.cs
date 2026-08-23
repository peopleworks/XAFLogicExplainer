namespace XafLogicExplainer.Core.Models;

/// <summary>Where a report's layout was read from.</summary>
public enum ReportLayoutSource
{
    /// <summary>The <c>InitializeComponent</c> of a <c>*.Designer.cs</c> — Visual Studio's designer.</summary>
    DesignerCode,

    /// <summary>A <c>.repx</c> file — the End-User Designer, or a layout exported from the running application.</summary>
    Repx,

    /// <summary>Assignments in the report class's own code, usually its constructor.</summary>
    Code,
}

/// <summary>
/// What a report shows, read from its layout.
/// </summary>
/// <remarks>
/// The filter, the expressions behind the labels, the grouping and the calculated fields are the
/// business decisions a report carries — "approved lines only", "net of VAT", "grouped by
/// region" — and nothing but the report engine ever reads them. Position, size, fonts and colours
/// are deliberately not here: they say how the page looks, not what it means.
/// </remarks>
public class ReportLayout
{
    public ReportLayoutSource Source { get; set; }

    /// <summary>The file the layout was read from.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>1-based line of the class or method the layout was read from; 0 for a <c>.repx</c>.</summary>
    public int Line { get; set; }

    /// <summary>
    /// The data source component's type, short: <c>CollectionDataSource</c>, <c>ViewDataSource</c>,
    /// <c>SqlDataSource</c>, … Null when the layout names none.
    /// </summary>
    public string? DataSourceKind { get; set; }

    /// <summary>
    /// What the data source points at: the entity (short name) for a <c>CollectionDataSource</c>,
    /// the view id for a <c>ViewDataSource</c>, the component's name for anything else.
    /// </summary>
    /// <remarks>
    /// A SQL data source carries its query and its connection as a Base64 blob, and the
    /// connection can carry credentials. Neither is decoded: the name is what this records.
    /// </remarks>
    public string? DataSource { get; set; }

    /// <summary>The member of the data source the report is bound to, when it names one.</summary>
    public string? DataMember { get; set; }

    /// <summary>The report-level filter, exactly as written.</summary>
    public string? FilterString { get; set; }

    /// <summary>Every expression bound to a control property, in source order.</summary>
    public List<ReportBinding> Bindings { get; set; } = [];

    /// <summary>Fields the report groups on, in order.</summary>
    public List<string> GroupFields { get; set; } = [];

    /// <summary>Fields the report computes rather than reads.</summary>
    public List<ReportCalculatedField> CalculatedFields { get; set; } = [];

    /// <summary>The report's own parameters.</summary>
    public List<ReportParameter> Parameters { get; set; } = [];
}

/// <summary>One expression bound to one property of one control.</summary>
public class ReportBinding
{
    /// <summary>The control's name, as the designer named it.</summary>
    public string Control { get; set; } = string.Empty;

    /// <summary>The bound property — nearly always <c>Text</c>.</summary>
    public string Property { get; set; } = string.Empty;

    /// <summary>The expression, exactly as written.</summary>
    public string Expression { get; set; } = string.Empty;
}

/// <summary>A field the report computes from the others.</summary>
public class ReportCalculatedField
{
    public string Name { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
}

/// <summary>A parameter the report declares.</summary>
public class ReportParameter
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The CLR type, short: <c>DateTime</c>, <c>String</c>, or an entity name.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the report viewer shows its own prompt for it. True unless the layout says
    /// otherwise — a parameter is visible when added, per the XtraReports documentation.
    /// </summary>
    /// <remarks>
    /// Matters in XAF: a visible parameter makes the viewer show its own panel, bypassing the
    /// <c>ReportParametersObjectBase</c> dialog the application registered for the report.
    /// </remarks>
    public bool Visible { get; set; } = true;

    public string? Description { get; set; }
}
