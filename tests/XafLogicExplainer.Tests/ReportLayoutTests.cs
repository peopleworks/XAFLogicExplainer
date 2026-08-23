using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That a report's layout is read from wherever the application keeps it — designer code, a
/// <c>.repx</c>, or a constructor — and says what the report shows.
/// </summary>
/// <remarks>
/// The filter, the bound expressions, the grouping and the calculated fields are business
/// decisions that exist nowhere else in the application: nothing reads them at run time but the
/// report engine. A reader asking "why does the invoice only list approved lines" is asking for
/// <c>FilterString</c>, and until now the answer was in a file the extractor skipped.
/// </remarks>
public class ReportLayoutTests
{
    private static ExtractedReport Report(string displayName) =>
        SampleProjects.Reports.Reports.Single(r => r.DisplayName == displayName);

    [Fact]
    public void DesignerCodeIsRead_AndSaysWhichFileItIs()
    {
        // Pins that BuildOutputFilter lets a *.Designer.cs through: generated, but not build
        // output, and the only place this layout exists.
        var layout = Report("Invoice").Layout;

        Assert.NotNull(layout);
        Assert.Equal(ReportLayoutSource.DesignerCode, layout.Source);
        Assert.EndsWith("InvoiceReport.Designer.cs", layout.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void DesignerCodeSaysWhatTheReportShows()
    {
        var layout = Report("Invoice").Layout!;

        Assert.Equal("CollectionDataSource", layout.DataSourceKind);
        Assert.Equal("Invoice", layout.DataSource);
        Assert.Equal("[IsApproved] = True And [Date] >= ?From", layout.FilterString);

        Assert.Equal(
            [("xrLabelCustomer", "Text", "[Customer.Name]"),
             ("xrLabelNumber", "Text", "[Number]"),
             ("xrLabelNet", "Text", "FormatString('{0:c}', [NetTotal])")],
            layout.Bindings.Select(b => (b.Control, b.Property, b.Expression)).ToList());

        Assert.Equal(["Customer.Name"], layout.GroupFields);
        Assert.Equal([("NetTotal", "[Total] / 1.21")],
            layout.CalculatedFields.Select(c => (c.Name, c.Expression)).ToList());
    }

    [Fact]
    public void DesignerCodeParametersAreRead_WithWhetherTheViewerShowsThem()
    {
        var parameters = Report("Invoice").Layout!.Parameters;
        var from = parameters[0];

        Assert.Equal("From", from.Name);
        Assert.Equal("DateTime", from.Type);
        Assert.False(from.Visible);
        Assert.Equal("Invoices dated on or after", from.Description);

        // typeof(string) and a .repx's System.String are the same type and must read the same,
        // or a renderer comparing the two would call them different.
        Assert.Equal(("Region", "String"), (parameters[1].Name, parameters[1].Type));
    }

    [Fact]
    public void ARepxIsRead_ThroughTheResourceNameTheClassLoads()
    {
        var layout = Report("Customer statement").Layout;

        Assert.NotNull(layout);
        Assert.Equal(ReportLayoutSource.Repx, layout.Source);
        Assert.EndsWith("CustomerStatement.repx", layout.FilePath, StringComparison.Ordinal);

        Assert.Equal("CollectionDataSource", layout.DataSourceKind);
        Assert.Equal("Customer", layout.DataSource);
        Assert.Equal("[IsApproved] = True", layout.FilterString);
        Assert.Equal(["Region"], layout.GroupFields);
        Assert.Equal([("InvoiceCount", "Count([Invoices])")],
            layout.CalculatedFields.Select(c => (c.Name, c.Expression)).ToList());
        Assert.Contains(layout.Bindings, b => b.Control == "xrLabelRegion" && b.Expression == "[Region]");
    }

    [Fact]
    public void ARepxParameterTypeIsResolvedThroughObjectStorage_AndDefaultsToString()
    {
        // Type="#Ref-3" points into <ObjectStorage>; a parameter with no Type attribute is a
        // string. Both shapes come from real .repx files, not from the designer's documentation.
        var parameters = Report("Customer statement").Layout!.Parameters;

        Assert.Equal([("PeriodStart", "DateTime"), ("Region", "String")],
            parameters.Select(p => (p.Name, p.Type)).ToList());
    }

    [Fact]
    public void AReportBuiltInItsConstructorIsReadFromTheConstructor()
    {
        var layout = Report("Overdue invoices").Layout;

        Assert.NotNull(layout);
        Assert.Equal(ReportLayoutSource.Code, layout.Source);
        Assert.Equal("Invoice", layout.DataSource);
        Assert.Equal("[IsApproved] = True And [Date] < AddDays(Today(), -30)", layout.FilterString);
        Assert.Equal([("number", "Text", "[Number]")],
            layout.Bindings.Select(b => (b.Control, b.Property, b.Expression)).ToList());
    }

    [Fact]
    public void TwoRegistrationsOfOneReportTypeEachCarryTheLayout()
    {
        Assert.NotNull(Report("Customer statement").Layout);
        Assert.NotNull(Report("Customer statement (in place)").Layout);
    }

    [Fact]
    public void ALayoutOnDiskThatNothingRegistersIsStillListed_AnywhereInTheSolution()
    {
        // A shop that designs reports outside Visual Studio ends up with .repx files that no
        // code names — fourteen of them, in one production application, in a Reporting folder
        // beside the module rather than in it. Silently skipping them would report an
        // application with fewer reports than its repository holds.
        var orphan = Assert.Single(SampleProjects.Reports.UnregisteredReportLayouts);

        Assert.EndsWith(Path.Combine("Reporting", "RegionSummary.repx"), orphan.FilePath, StringComparison.Ordinal);

        // Exported from the running application, this one keeps its data source in
        // <ObjectStorage> rather than <ComponentStorage>; the #Ref has to be followed into both.
        Assert.Equal("SqlDataSource", orphan.DataSourceKind);
        Assert.Equal("Warehouse", orphan.DataSource);
        Assert.Equal("Summary", orphan.DataMember);
    }

    [Fact]
    public void TheSearchClimbsToTheSolutionOnlyFromAProject()
    {
        // Pointed at a folder that is not a project, the search stays in it. Found by pointing the
        // analyzer at a solution root whose parent held every other repository on the machine:
        // seventeen layouts, none of them this application's.
        var reportingFolder = Path.Combine(Path.GetDirectoryName(SampleProjects.ReportsPath)!, "Reporting");

        var extracted = new Core.Analyzers.LogicExtractor()
            .ExtractFromSourceDirectory(reportingFolder, new ExtractionOptions { DiscoverPlatformModels = false });

        Assert.Single(extracted.UnregisteredReportLayouts);
    }
}
