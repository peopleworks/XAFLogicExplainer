namespace XafLogicExplainer.Core.Models;

/// <summary>
/// A predefined report, as its registration declares it.
/// </summary>
/// <remarks>
/// Reports V2 registers a report in code — <c>PredefinedReportsUpdater.AddPredefinedReport&lt;T&gt;</c>
/// in the module's <c>GetModuleUpdaters</c> — and that one call is the only place the application
/// says which reports it has, what each is called, and which entity it is over. The layout and
/// the parameters live elsewhere and are read separately.
/// <para>
/// What this deliberately does not cover: reports users create or copy at run time. Those live
/// only in <c>ReportDataV2</c> rows in the database, and no file in the repository knows they
/// exist. A document built from this list must say so.
/// </para>
/// </remarks>
public class ExtractedReport
{
    /// <summary>The name the Reports list shows.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The <c>XtraReport</c> descendant that carries the layout: the generic argument.</summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>The business object the report is over: the <c>typeof</c> in the second slot.</summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// The <c>ReportParametersObjectBase</c> descendant XAF shows as a dialog before the report
    /// opens, when the registration names one.
    /// </summary>
    public string? ParametersType { get; set; }

    /// <summary>
    /// Whether the report is offered in place, on the data type's list view. Null when the
    /// overload used does not say — the two-argument form is silent on it, and a default here
    /// would read as a decision nobody made.
    /// </summary>
    public bool? IsInplaceReport { get; set; }

    /// <summary>
    /// What the report shows, read from its designer code, its <c>.repx</c> or its constructor.
    /// Null when the report type's declaration was not found under the project — it lives in
    /// another assembly, or the layout is loaded from somewhere the code does not name.
    /// </summary>
    public ReportLayout? Layout { get; set; }

    /// <summary>
    /// The parameters dialog, when the registration names one and its class is declared under
    /// the project.
    /// </summary>
    public ReportParametersObject? ParametersObject { get; set; }

    /// <summary>File the registration is in.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>1-based line of the registration.</summary>
    public int Line { get; set; }
}
