using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Core.Analyzers;

/// <summary>
/// Reads the predefined reports an application registers.
/// </summary>
/// <remarks>
/// The registration is one invocation shape —
/// <c>updater.AddPredefinedReport&lt;TReport&gt;(displayName, dataType[, parametersObjectType][, isInplaceReport])</c>
/// — documented to live in the module's <c>GetModuleUpdaters</c>, though nothing forces it there:
/// a shop with many reports may register them from a helper, or from a second module. So this
/// scans every analyzable file for the call rather than trusting one method in one class,
/// the way the updater analyzer falls back to looking for what actually matters.
/// <para>
/// Syntax only, like everything else here: no <c>DevExpress.ExpressApp.ReportsV2</c> reference
/// and no compilation. What it reads is what the call says, and a slot the overload does not
/// have stays null rather than being defaulted.
/// </para>
/// </remarks>
public class ReportAnalyzer
{
    private const string RegistrationMethod = "AddPredefinedReport";

    /// <summary>
    /// Every <c>AddPredefinedReport&lt;T&gt;(...)</c> call under the directory, in source order.
    /// </summary>
    public List<ExtractedReport> AnalyzeRegistrations(string sourceDirectory, ExtractionOptions options)
    {
        var reports = new List<ExtractedReport>();

        foreach (var file in AnalyzableFiles(sourceDirectory).Where(f => ContainsWord(f, RegistrationMethod)))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file).GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (ReadRegistration(invocation, file) is { } report)
                    reports.Add(report);
            }
        }

        return reports;
    }

    /// <summary>
    /// Whether any analyzable file names <c>ReportsModuleV2</c> — as a required module, in a
    /// <c>AddReports</c> call, or anywhere else.
    /// </summary>
    /// <remarks>
    /// The question this answers is not "are there reports" but "could there be reports this
    /// extraction cannot see": with the module in, users create reports at run time that live only
    /// in the database.
    /// </remarks>
    public bool ReferencesReportsModule(string sourceDirectory) =>
        AnalyzableFiles(sourceDirectory).Any(f => ContainsWord(f, "ReportsModuleV2"));

    private static ExtractedReport? ReadRegistration(InvocationExpressionSyntax invocation, string file)
    {
        // updater.AddPredefinedReport<TReport>(...)  — the generic name is the report type.
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax { Identifier.Text: RegistrationMethod } generic,
            })
            return null;

        // ponytail: the non-generic AddPredefinedReport(IReportDataV2) overload takes an object
        // built any way the caller likes; reading it means reading arbitrary construction. Left
        // out until a real application shows the shape it actually takes.
        if (generic.TypeArgumentList.Arguments.Count != 1)
            return null;

        var args = invocation.ArgumentList.Arguments;
        if (args.Count < 2)
            return null;

        var report = new ExtractedReport
        {
            ReportType = ShortName(generic.TypeArgumentList.Arguments[0]),
            DisplayName = SyntaxLiteral.ValueOf(args[0].Expression),
            DataType = TypeOfName(args[1].Expression) ?? args[1].Expression.ToString(),
            FilePath = file,
            Line = SourceLine.Of(invocation.GetFirstToken()),
        };

        // Slots 3 and 4 are told apart by shape, not position: a typeof is the parameters object,
        // a boolean is the in-place flag. (name, type, bool) and (name, type, type) are both
        // three arguments long.
        foreach (var arg in args.Skip(2))
        {
            if (TypeOfName(arg.Expression) is { } parametersType)
                report.ParametersType = parametersType;
            else if (arg.Expression.Kind() is SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression)
                report.IsInplaceReport = arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        return report;
    }

    private static string? TypeOfName(ExpressionSyntax expression) =>
        expression is TypeOfExpressionSyntax typeOf ? ShortName(typeOf.Type) : null;

    /// <summary>
    /// <c>Invoicing.Module.Reports.InvoiceReport</c> → <c>InvoiceReport</c>, matching how entity
    /// and controller names are recorded elsewhere in the extraction.
    /// </summary>
    private static string ShortName(TypeSyntax type) =>
        type is QualifiedNameSyntax qualified ? qualified.Right.ToString() : type.ToString();

    private static IEnumerable<string> AnalyzableFiles(string sourceDirectory) =>
        Directory.Exists(sourceDirectory)
            ? Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(f => BuildOutputFilter.IsAnalyzable(f, sourceDirectory))
            : [];

    /// <summary>Cheap gate before parsing: a file without the word cannot contain the call.</summary>
    private static bool ContainsWord(string file, string word)
    {
        try
        {
            return File.ReadAllText(file).Contains(word, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }
}
