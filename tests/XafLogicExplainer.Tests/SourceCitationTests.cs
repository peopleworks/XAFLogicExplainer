using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That a citation names a file the same way however the project path was typed.
/// </summary>
/// <remarks>
/// <c>--project C:/Apps/App.Module</c> from a bash prompt and <c>--project C:\Apps\App.Module</c> from
/// PowerShell are one directory. The citation compared the strings as typed, so the first cited a
/// layout inside the module as <c>../App.Module/Reports/Statement.repx</c> and the second as
/// <c>Reports/Statement.repx</c>. A committed document would change its citations depending on
/// which shell last regenerated it, which is the diff <see cref="SourceCitation"/> exists to prevent.
/// </remarks>
public class SourceCitationTests
{
    private static readonly string Solution = Path.Combine(Path.GetTempPath(), "xle-citation", "App");
    private static readonly string Module = Path.Combine(Solution, "App.Module");

    [Fact]
    public void CitesAFileInsideTheProjectTheSameWhicheverSeparatorWasTyped()
    {
        var file = Path.Combine(Module, "Reports", "Statement.repx");

        var typedWithForwardSlashes = new ExtractedProject { ProjectPath = Module.Replace('\\', '/') };
        var typedWithTheSystemSeparator = new ExtractedProject { ProjectPath = Module };

        Assert.Equal("`Reports/Statement.repx`", SourceCitation.Of(typedWithForwardSlashes, file, 0));
        Assert.Equal("`Reports/Statement.repx:12`", SourceCitation.Of(typedWithTheSystemSeparator, file.Replace('\\', '/'), 12));
    }

    [Fact]
    public void CitesAFileBesideTheProjectRelativeToTheSolutionWhicheverSeparatorWasTyped()
    {
        var layout = Path.Combine(Solution, "Reporting", "Summary.repx");
        var project = new ExtractedProject { ProjectPath = Module.Replace('\\', '/') };

        Assert.Equal("`../Reporting/Summary.repx`", SourceCitation.Of(project, layout, 0));
    }

    [Fact]
    public void ASiblingWhoseNameStartsWithTheProjectsIsNotInsideIt()
    {
        // App.Module2 begins with the characters of App.Module. Normalising separators must not
        // loosen the boundary check that keeps it outside.
        var file = Path.Combine(Solution, "App.Module2", "Thing.cs");
        var project = new ExtractedProject { ProjectPath = Module.Replace('\\', '/') + "/" };

        Assert.Equal("`../App.Module2/Thing.cs`", SourceCitation.Of(project, file, 0));
    }
}
