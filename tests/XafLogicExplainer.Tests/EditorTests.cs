using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Editors the team wrote, which make a screen differ from what its property type implies.
/// </summary>
/// <remarks>
/// The demo solution mirrors the real shape: the editor lives in a <c>*.Blazor.Server</c> project
/// beside the module, and the alias it names is a constant declared elsewhere.
/// </remarks>
public class EditorTests
{
    private static ExtractedProject Demo => SampleProjects.Demo;

    [Fact]
    public void FindsAnEditorInASiblingPlatformProject()
    {
        // The thing that makes editors hard to notice: they are not in the module at all.
        var editor = Assert.Single(Demo.Editors, e => e.ClassName == "BarcodeScannerPropertyEditor");

        Assert.Equal(EditorKind.PropertyEditor, editor.Kind);
        Assert.Contains("Blazor.Server", editor.SourceProject);
    }

    [Fact]
    public void ReadsWhatTheEditorRenders()
    {
        var editor = Demo.Editors.Single(e => e.ClassName == "BarcodeScannerPropertyEditor");

        Assert.Equal("string", editor.TargetType);
        Assert.Equal("BlazorPropertyEditorBase", editor.BaseType);
    }

    [Fact]
    public void ResolvesAnAliasConstantDeclaredInAnotherFile()
    {
        // The attribute reads CustomEditorAliases.BarcodeScannerPropertyEditor. Reporting that
        // verbatim leaks an implementation detail where the reader needs the value XAF matches on.
        var editor = Demo.Editors.Single(e => e.ClassName == "BarcodeScannerPropertyEditor");

        Assert.Equal("BarcodeScannerPropertyEditor", editor.Alias);
    }

    [Fact]
    public void RecordsTheClientFilesAnEditorCannotWorkWithout()
    {
        var editor = Demo.Editors.Single(e => e.ClassName == "BarcodeScannerPropertyEditor");

        Assert.Contains(editor.ClientAssets, a => a.EndsWith("barcode-scanner.js", StringComparison.Ordinal));
    }

    [Fact]
    public void DoesNotClaimAnEditorIsUsedWhereItIsOnlySelectable()
    {
        // [PropertyEditor(typeof(string), alias, false)] makes the editor *selectable* in the
        // Model Editor. Listing every entity with a string property as "uses the barcode scanner"
        // would be plainly false, and the kind of confident wrongness this project exists to stop.
        var editor = Demo.Editors.Single(e => e.ClassName == "BarcodeScannerPropertyEditor");

        Assert.False(editor.IsDefault);
        Assert.Empty(editor.UsedBy);
    }

    [Fact]
    public void LinksAnEditorThatReplacesTheDefaultForItsType()
    {
        var project = ProjectWith(
            new ExtractedEditor { ClassName = "MoneyEditor", TargetType = "decimal", IsDefault = true });

        Assert.Contains("Invoice", project.Editors.Single().UsedBy);
    }

    [Fact]
    public void NeverLinksAnEditorRegisteredForEveryType()
    {
        // [PropertyEditor(typeof(object), …)] claims everything; naming every property in the
        // application communicates nothing.
        var project = ProjectWith(
            new ExtractedEditor { ClassName = "CatchAll", TargetType = "object", IsDefault = true });

        Assert.Empty(project.Editors.Single().UsedBy);
    }

    [Fact]
    public void FindsBuiltInEditorsReconfiguredByAController()
    {
        // No custom editor class exists for these: a controller reaches into a built-in editor's
        // component model at run time, leaving no trace on the entity or in the Model Editor.
        var controller = Assert.Single(
            Demo.Controllers, c => c.ClassName == "CustomizeExpiryEditorController");

        Assert.Contains("DateTimePropertyEditor", controller.CustomizedEditors);
    }

    [Fact]
    public void ApplicationsWithoutEditorsAreUnaffected()
    {
        Assert.Empty(SampleProjects.Xpo.Editors);
        Assert.All(SampleProjects.Xpo.Controllers, c => Assert.Empty(c.CustomizedEditors));
    }

    // ------------------------------------------------------------- surfacing

    [Fact]
    public void TheAgentContextWarnsThatScreensMayNotFollowTheirTypes()
    {
        var index = new AgentContextGenerator("0.10.1").GenerateIndex(Demo, []);

        Assert.Contains("Custom editors", index);
        Assert.Contains("BarcodeScannerPropertyEditor", index);
        Assert.Contains("do not show the control their type implies", index);
        Assert.Contains("CustomizeExpiryEditorController", index);
    }

    [Fact]
    public void TheExplainerShowsEditorsAndWhatTheyNeed()
    {
        var html = new HtmlExplainerGenerator("0.10.1").Generate(Demo);

        Assert.Contains("id=\"editors\"", html);
        Assert.Contains("BarcodeScannerPropertyEditor", html);
        Assert.Contains("barcode-scanner.js", html);
        Assert.Contains("reconfigured at run time", html);
    }

    // ------------------------------------------------------------ raw reader

    [Fact]
    public void CollectsConstantsByNameAndByTypeName()
    {
        var constants = EditorAnalyzer.CollectStringConstants(SampleProjects.DemoBlazorPath);

        Assert.Equal("BarcodeScannerPropertyEditor", constants["BarcodeScannerPropertyEditor"]);
        Assert.Equal(
            "BarcodeScannerPropertyEditor",
            constants["CustomEditorAliases.BarcodeScannerPropertyEditor"]);
    }

    private static ExtractedProject ProjectWith(ExtractedEditor editor)
    {
        var project = new ExtractedProject
        {
            ProjectName = "Sample",
            Entities =
            {
                new ExtractedEntity
                {
                    ClassName = "Invoice",
                    Properties = { new ExtractedProperty { Name = "Total", TypeName = "decimal" } },
                },
            },
            Editors = { editor },
        };

        LogicExtractor.LinkEditorsToProperties(project);
        return project;
    }
}
