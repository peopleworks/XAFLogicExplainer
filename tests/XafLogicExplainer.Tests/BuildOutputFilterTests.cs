using XafLogicExplainer.Core.Analyzers;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Skipping build output without skipping real source.
/// </summary>
/// <remarks>
/// Regression cover for substring matching: five analyzers tested
/// <c>path.Contains("bin")</c>, so any project whose path merely contained those letters had every
/// file skipped and was reported as an application with no entities at all — with nothing in the
/// output to explain why.
/// </remarks>
public class BuildOutputFilterTests
{
    [Theory]
    [InlineData(@"C:\Solution\App.Module\bin\Debug\net10.0\App.dll")]
    [InlineData(@"C:\Solution\App.Module\obj\Debug\App.AssemblyInfo.cs")]
    [InlineData("/home/dev/app/bin/Release/App.dll")]
    [InlineData("/home/dev/app/obj/project.assets.json")]
    [InlineData(@"App.Module\bin\x.cs")]
    public void SkipsBuildOutput(string path) =>
        Assert.True(BuildOutputFilter.IsBuildOutput(path));

    [Theory]
    [InlineData(@"C:\Projects\robinson\App.Module\Order.cs")]
    [InlineData(@"C:\Projects\Cabin\App.Module\Booking.cs")]
    [InlineData("/srv/objects/App.Module/Customer.cs")]
    [InlineData(@"C:\Solution\Binder.Module\BusinessObjects\Folder.cs")]
    public void KeepsSourceWhoseNameMerelyContainsThoseLetters(string path) =>
        Assert.False(BuildOutputFilter.IsBuildOutput(path));

    [Fact]
    public void KeepsProjectsThatLiveUnderADirectoryNamedBin()
    {
        // Build output is always below the project root. A "bin" above it is somebody's folder
        // name, and skipping those files would report an application with nothing in it.
        const string root = @"C:\bin\Sales\App.Module";

        Assert.False(BuildOutputFilter.IsBuildOutput($@"{root}\BusinessObjects\Invoice.cs", root));
        Assert.True(BuildOutputFilter.IsBuildOutput($@"{root}\bin\Debug\net10.0\App.dll", root));
    }

    [Fact]
    public void IgnoresCase() =>
        Assert.True(BuildOutputFilter.IsBuildOutput(@"C:\App\BIN\Debug\x.cs"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsMissingPathsAsAnalyzable(string? path) =>
        Assert.False(BuildOutputFilter.IsBuildOutput(path));

    [Fact]
    public void IsAnalyzableIsTheInverse()
    {
        Assert.True(BuildOutputFilter.IsAnalyzable(@"C:\Projects\Sales\App\Order.cs"));
        Assert.False(BuildOutputFilter.IsAnalyzable(@"C:\App\bin\Debug\Order.cs"));
    }
}
