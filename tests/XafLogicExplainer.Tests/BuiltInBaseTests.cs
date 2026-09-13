using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Business classes whose base is a class DevExpress ships, or that name no base at all (#82).
/// </summary>
/// <remarks>
/// Measured on six real applications before any of this was written: the largest lost twelve classes
/// of its own module, the one its staffing screens are built on among them, and every association
/// pointing at one of them was reported with a single end while both ends were in the source.
/// </remarks>
public class BuiltInBaseTests
{
    [Fact]
    public void AClassDerivingFromPersonIsListed() =>
        Assert.Contains("Employee", Listed);

    [Fact]
    public void AClassDerivingFromEventIsListed() =>
        Assert.Contains("Shift", Listed);

    [Fact]
    public void TheReportAndDashboardStorageClassesAreListed()
    {
        Assert.Contains("StaffingReport", Listed);
        Assert.Contains("StaffingDashboard", Listed);
    }

    [Fact]
    public void AnAssociationWithAClassDerivingFromPersonHasBothEnds() =>
        Assert.Equal(2, Ends("Department-Employees"));

    [Fact]
    public void AnAttachmentWithNoClassAttributeStillHasBothEndsOfItsAssociation() =>
        Assert.Equal(2, Ends("Resume-Attachments"));

    [Fact]
    public void AClassFromTheLibraryIsPersistent() =>
        Assert.True(Staffing.Entity("Employee").IsPersistent);

    [Fact]
    public void ANonPersistentBaseObjectIsListedAsNotPersistent() =>
        Assert.False(Staffing.Entity("StaffingBoard").IsPersistent);

    [Fact]
    public void ADescendantOfANonPersistentClassIsNotPersistentEither() =>
        Assert.False(Staffing.Entity("OvertimeBoard").IsPersistent);

    [Fact]
    public void ADomainComponentWithNoBaseIsListedAsNotPersistent() =>
        Assert.False(Staffing.Entity("ShiftSettings").IsPersistent);

    [Fact]
    public void AReportParametersObjectStaysWithItsReport() =>
        Assert.DoesNotContain("PayrollParameters", Listed);

    [Fact]
    public void AClassWithNeitherAnAttributeNorABaseIsNotListed() =>
        Assert.DoesNotContain("PayrollCalculator", Listed);

    [Fact]
    public void AClassTheApplicationDeclaresDecidesWhatItsNameMeans() =>
        Assert.DoesNotContain("ReviewNote", Listed);

    [Fact]
    public void ANonPersistentClassGetsTheViewsXafGeneratesForIt()
    {
        var ids = Staffing.Views.Select(view => view.Id).ToList();

        Assert.Contains("StaffingBoard_ListView", ids);
        Assert.Contains("StaffingBoard_DetailView", ids);
        Assert.Contains("StaffingBoard_LookupListView", ids);
        Assert.Contains("StaffingBoard_Available_ListView", ids);
    }

    [Fact]
    public void ACustomizedNestedViewOfANonPersistentClassIsNotReportedAsHandWritten()
    {
        // The Model Editor file changes a column of this view. With the class missing, the view was
        // not generated, so the customization was taken for a view somebody wrote by hand.
        var view = Staffing.Views.Single(candidate => candidate.Id == "StaffingBoard_Available_ListView");

        Assert.Equal(ViewOrigin.Customized, view.Origin);
        Assert.Equal("Employee", view.ObjectType);
    }

    [Fact]
    public void TheReportsFixtureListsItsDialogsWithTheReportsAndNotWithTheClasses()
    {
        var names = SampleProjects.Reports.Entities.Select(entity => entity.ClassName).ToList();

        Assert.DoesNotContain("StatementParameters", names);
        Assert.DoesNotContain("OverdueParameters", names);
    }

    private static ExtractedProject Staffing => SampleProjects.BuiltInBases;

    private static List<string> Listed => [.. Staffing.Entities.Select(entity => entity.ClassName)];

    private static int Ends(string association) =>
        Staffing.Entities
            .SelectMany(entity => entity.Relationships)
            .Count(relationship => relationship.InheritedFrom is null && relationship.AssociationName == association);
}
