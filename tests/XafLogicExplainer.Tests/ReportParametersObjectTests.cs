using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That the dialog XAF shows before a report opens — the <c>ReportParametersObjectBase</c>
/// descendant — is read, with what its parameters mean.
/// </summary>
/// <remarks>
/// The class is the report's front door: every property is a field in the dialog, and
/// <c>GetCriteria()</c> is business logic in the plainest sense — <em>this report shows invoices
/// dated in the period and, when a customer is chosen, only that customer's</em>. It is not an
/// entity and not a controller, so until now it was nothing.
/// </remarks>
public class ReportParametersObjectTests
{
    private static ExtractedReport Report(string displayName) =>
        SampleProjects.Reports.Reports.Single(r => r.DisplayName == displayName);

    [Fact]
    public void TheRegisteredParametersObjectIsAttachedToItsReport_AndSaysWhereItIs()
    {
        var parameters = Report("Customer statement").ParametersObject;

        Assert.NotNull(parameters);
        Assert.Equal("StatementParameters", parameters.ClassName);
        Assert.EndsWith("StatementParameters.cs", parameters.FilePath, StringComparison.Ordinal);
        Assert.Equal(15, parameters.Line);

        Assert.Null(Report("Invoice").ParametersObject);
    }

    [Fact]
    public void EveryPropertyIsAParameter_WithItsTypeAndDefault()
    {
        var fields = Report("Customer statement").ParametersObject!.Fields;

        Assert.Equal(
            [("Customer", "Customer?", null),
             ("From", "DateTime", "DateTime.Today.AddMonths(-1)"),
             ("To", "DateTime", "DateTime.Today"),
             ("ApprovedOnly", "bool", "true")],
            fields.Select(f => (f.Name, f.Type, f.Default)).ToList());
    }

    [Fact]
    public void TheCriteriaAndSortingAreKeptAsSource()
    {
        var parameters = Report("Customer statement").ParametersObject!;

        Assert.Contains("Customer.Oid = ?", parameters.CriteriaSource, StringComparison.Ordinal);
        Assert.Contains("if (ApprovedOnly)", parameters.CriteriaSource, StringComparison.Ordinal);
        Assert.Contains("new SortProperty(\"Date\"", parameters.SortingSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AParametersObjectNothingRegistersIsStillListed()
    {
        var orphan = Assert.Single(SampleProjects.Reports.UnregisteredReportParameters);

        Assert.Equal("OverdueParameters", orphan.ClassName);
        Assert.Equal([("DaysOverdue", "int", "30")], orphan.Fields.Select(f => (f.Name, f.Type, f.Default)).ToList());
        Assert.Contains("Date < ?", orphan.CriteriaSource, StringComparison.Ordinal);
    }
}
