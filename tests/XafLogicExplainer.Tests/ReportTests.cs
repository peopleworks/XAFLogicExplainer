using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That the reports an application registers are read, with what the registration says about
/// each — and nothing it does not say.
/// </summary>
/// <remarks>
/// Reports V2 registers a predefined report in one invocation shape,
/// <c>PredefinedReportsUpdater.AddPredefinedReport&lt;TReport&gt;(displayName, dataType, ...)</c>,
/// in four overloads. The two- and four-argument forms differ in meaning, not only in length: the
/// short one says nothing about in-place, so the model carries <c>null</c> there rather than a
/// default that would read as a decision nobody made.
/// </remarks>
public class ReportTests
{
    private static ExtractedProject Project => SampleProjects.Reports;

    [Fact]
    public void EveryRegistrationIsRead_InDeclarationOrder()
    {
        var names = Project.Reports.Select(r => r.DisplayName).ToList();

        Assert.Equal(
            ["Invoice", "Overdue invoices", "Customer statement", "Customer statement (in place)"],
            names);
    }

    [Fact]
    public void TheReportTypeAndTheEntityItIsOverAreRead()
    {
        var statement = Project.Reports.Single(r => r.DisplayName == "Customer statement");

        Assert.Equal("CustomerStatementReport", statement.ReportType);
        Assert.Equal("Customer", statement.DataType);
    }

    [Fact]
    public void TheParametersObjectIsReadWhenTheOverloadNamesOne()
    {
        Assert.Null(Project.Reports.Single(r => r.DisplayName == "Invoice").ParametersType);
        Assert.Equal("StatementParameters",
            Project.Reports.Single(r => r.DisplayName == "Customer statement").ParametersType);
    }

    [Fact]
    public void InPlaceIsNullWhenTheOverloadDoesNotSay()
    {
        var byName = Project.Reports.ToDictionary(r => r.DisplayName);

        Assert.Null(byName["Invoice"].IsInplaceReport);
        Assert.True(byName["Overdue invoices"].IsInplaceReport);
        Assert.Null(byName["Customer statement"].IsInplaceReport);
        Assert.False(byName["Customer statement (in place)"].IsInplaceReport);
    }

    [Fact]
    public void EachRegistrationSaysWhereItIs()
    {
        var invoice = Project.Reports.Single(r => r.DisplayName == "Invoice");

        Assert.EndsWith("Module.cs", invoice.FilePath, StringComparison.Ordinal);
        Assert.Equal(34, invoice.Line);
    }

    [Fact]
    public void AnApplicationWithoutTheReportsModuleSaysSo()
    {
        Assert.True(Project.ReferencesReportsModule);

        Assert.False(SampleProjects.Xpo.ReferencesReportsModule);
        Assert.Empty(SampleProjects.Xpo.Reports);
    }
}
