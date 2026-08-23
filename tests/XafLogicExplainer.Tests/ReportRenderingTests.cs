using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// How the reports an application declares are written down, and how the list says what it is not.
/// </summary>
/// <remarks>
/// The extraction (#37, phases 1–3) is <see href="https://github.com/MBrekhof">@MBrekhof</see>'s.
/// This is the other half: the rendering, and the sentence that makes the list safe to believe.
/// <para>
/// That sentence carries more than the list does. Reports V2 lets users build reports at run time in
/// the End-User Designer, and those are stored as database rows rather than files, so an extraction
/// that reads source can never see them. Checked against a production application which registers
/// <c>ReportsModuleV2</c>, sets <c>ReportStoreMode.XML</c> with <c>ReportDataType = ReportDataV2</c>,
/// and contains no report in source at all: printing "no reports" there is not an incomplete answer
/// but a wrong one, and the more use an application makes of reports the wronger it gets.
/// </para>
/// </remarks>
public class ReportRenderingTests
{
    private static string Markdown(ExtractedProject project, string language = "en") =>
        string.Join("\n", new MarkdownDocumentationGenerator(language)
            .GenerateSections(project)
            .Select(section => section.Content));

    private static string? ReportsSection(ExtractedProject project, string language = "en") =>
        new MarkdownDocumentationGenerator(language)
            .GenerateSections(project)
            .FirstOrDefault(section => section.Title is "Reports" or "Reportes")
            ?.Content;

    /// <summary>The fixture, adjusted so one state can be told apart from another.</summary>
    private static ExtractedProject Reports(
        bool referencesModule = true, bool withReports = true, bool withUnregistered = true)
    {
        var project = SampleProjects.Extract(SampleProjects.ReportsPath);

        project.ReferencesReportsModule = referencesModule;

        if (!withReports)
            project.Reports = [];

        if (!withUnregistered)
        {
            project.UnregisteredReportLayouts = [];
            project.UnregisteredReportParameters = [];
        }

        return project;
    }

    // ------------------------------------------------- what the list says it is

    [Fact]
    public void WithReportsAndTheModuleTheListSaysItIsALowerBound()
    {
        var section = ReportsSection(Reports());

        Assert.NotNull(section);
        Assert.Contains("lower bound", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WithTheModuleAndNoReportsTheAnswerIsUnknownRatherThanZero()
    {
        // The case that motivated the whole sentence, and the most common one in the wild: the XAF
        // wizard registers ReportsModuleV2 whether or not the application ever gains a report, and
        // an application whose users design their own has all of them in the database.
        var section = ReportsSection(Reports(withReports: false));

        Assert.NotNull(section);
        Assert.Contains("declares no reports in source", section, StringComparison.Ordinal);
        Assert.Contains("not zero but unknown", section, StringComparison.Ordinal);
    }

    [Fact]
    public void WithoutTheModuleTheListIsComplete()
    {
        // No ReportsModuleV2 means no End-User Designer, so nothing can appear at run time and the
        // list really is all of them. Hedging here would be its own kind of dishonesty.
        var section = ReportsSection(Reports(referencesModule: false));

        Assert.NotNull(section);
        Assert.Contains("these are all of them", section, StringComparison.Ordinal);
        Assert.DoesNotContain("lower bound", section, StringComparison.Ordinal);
    }

    [Fact]
    public void AnApplicationWithoutReportsGetsNoSectionAtAll()
    {
        // "No reports" is worth printing when it is a finding. For an application that never
        // registered the module and ships no layout it is the default, and a section saying so
        // would be a paragraph about nothing in every document we generate.
        Assert.Null(ReportsSection(SampleProjects.Xpo));
        Assert.Null(ReportsSection(Reports(referencesModule: false, withReports: false, withUnregistered: false)));
    }

    // ------------------------------------------------- what the entries carry

    [Fact]
    public void AReportCarriesTheDecisionsInsideIt()
    {
        var section = ReportsSection(Reports());

        Assert.NotNull(section);

        // The filter is the most consequential line in a report and the one nothing else in the
        // repository mentions.
        Assert.Contains("[IsApproved] = True And [Date] >= ?From", section, StringComparison.Ordinal);
        Assert.Contains("[Total] / 1.21", section, StringComparison.Ordinal);
        Assert.Contains("Customer.Name", section, StringComparison.Ordinal);

        // And what the dialog turns its answers into, which is business logic in the plainest sense.
        Assert.Contains("GetCriteria()", section, StringComparison.Ordinal);
    }

    [Fact]
    public void NullInPlaceIsNotRenderedAsNo()
    {
        // The registration overload that says nothing about in-place reporting leaves XAF's own
        // default in charge. Printing "no" there would be an invention, so the line is absent.
        var project = Reports();
        var quiet = project.Reports.First(report => report.IsInplaceReport is null);
        var section = ReportsSection(project);

        Assert.NotNull(section);

        var heading = section.IndexOf($"### {quiet.DisplayName}", StringComparison.Ordinal);
        var next = section.IndexOf("\n###", heading + 1, StringComparison.Ordinal);
        var entry = next < 0 ? section[heading..] : section[heading..next];

        Assert.DoesNotContain("Offered in place", entry, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoRegistrationsOfOneLayoutAreNotPrintedTwice()
    {
        // The same `.repx` offered from the navigation and in place on a list view is an ordinary
        // pair. Repeating the filter, the bindings and the whole of GetCriteria() under the second
        // buries the only thing that differs between them, which is what the reader came for.
        var section = ReportsSection(Reports());

        Assert.NotNull(section);
        Assert.Contains("Same layout and dialog as", section, StringComparison.Ordinal);

        var criteriaBlocks = section.Split("GetCriteria()").Length - 1;
        Assert.Equal(1, criteriaBlocks);
    }

    [Fact]
    public void ALayoutNothingRegistersIsListedRatherThanSkipped()
    {
        // A shop that designs reports outside Visual Studio keeps the exports beside the module and
        // imports them by hand. Those files are real work that no registration claims, and silence
        // about them reads as their not existing.
        var section = ReportsSection(Reports());

        Assert.NotNull(section);
        Assert.Contains("Layouts nothing registers", section, StringComparison.Ordinal);
        Assert.Contains("RegionSummary.repx", section, StringComparison.Ordinal);
        Assert.Contains("OverdueParameters", section, StringComparison.Ordinal);
    }

    // ------------------------------------------------- citations

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void NoCitationNamesADriveOnThisMachine(string language)
    {
        // A layout kept beside the module rather than inside it is outside the project path, and
        // the first version of this printed its absolute path — the drive of whichever machine ran
        // the extraction, in a document meant to be committed and read elsewhere. Every regeneration
        // somewhere else would have rewritten it.
        var markdown = Markdown(Reports(), language);

        Assert.DoesNotContain(":/", markdown.Replace("https://", "").Replace("http://", ""),
            StringComparison.Ordinal);
        Assert.Contains("../Reporting/RegionSummary.repx", markdown, StringComparison.Ordinal);
    }

    // ------------------------------------------------- the other surfaces

    [Fact]
    public void TheAgentContextCarriesTheBoundRatherThanJustTheList()
    {
        var context = new AgentContextGenerator().GenerateIndex(Reports(), []);

        Assert.Contains("## Reports", context, StringComparison.Ordinal);
        Assert.Contains("lower bound", context, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAgentContextRefusesToSayAnApplicationHasNoReports()
    {
        // An agent reads this file and acts on it. Told "no reports", it designs as though none can
        // exist — for an application whose users may have built forty.
        var context = new AgentContextGenerator().GenerateIndex(Reports(withReports: false), []);

        Assert.Contains("no report in source", context, StringComparison.Ordinal);
        Assert.Contains("unknown, not zero", context, StringComparison.Ordinal);
    }
}
