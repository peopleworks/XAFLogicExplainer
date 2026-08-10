using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.Tests;

/// <summary>
/// The DevExpress ground-truth catalog, and what extraction does with it.
/// </summary>
/// <remarks>
/// Exercised against a synthetic catalog rather than a generated one, so the suite still needs no
/// DevExpress installation and produces the same result on every machine.
/// </remarks>
public class CatalogTests
{
    private static XafCatalog SampleCatalog => new()
    {
        DevExpressVersion = "26.1",
        GeneratedAt = "2026-08-10T00:00:00.0000000Z",
        Attributes =
        {
            ["DefaultClassOptionsAttribute"] = new XafCatalogType
            {
                Name = "DefaultClassOptionsAttribute",
                Namespace = "DevExpress.Persistent.Base",
                Assembly = "DevExpress.Persistent.Base",
            },
            ["IndexedAttribute"] = new XafCatalogType
            {
                Name = "IndexedAttribute",
                Namespace = "DevExpress.Xpo",
                Assembly = "DevExpress.Xpo",
            },
        },
        Controllers =
        {
            ["ViewController"] = new XafCatalogType
            {
                Name = "ViewController",
                Namespace = "DevExpress.ExpressApp",
                Assembly = "DevExpress.ExpressApp",
                Summary = "A View Controller.",
            },
            ["DeleteObjectsViewController"] = new XafCatalogType
            {
                Name = "DeleteObjectsViewController",
                Namespace = "DevExpress.ExpressApp.SystemModule",
                Assembly = "DevExpress.ExpressApp",
                BaseType = "ViewController",
                Summary = "Represents a ViewController descendant that contains the Delete Action.",
                DocumentationUrl = "https://docs.devexpress.com/eXpressAppFramework/112622/",
            },
        },
    };

    // ------------------------------------------------------------ lookups

    [Theory]
    [InlineData("DefaultClassOptions")]
    [InlineData("DefaultClassOptionsAttribute")]
    public void FindsAttributesWithOrWithoutTheSuffix(string written)
    {
        // C# lets [Description] stand for DescriptionAttribute, and source is written both ways.
        Assert.NotNull(SampleCatalog.FindAttribute(written));
    }

    [Fact]
    public void ReturnsNothingForAnUnknownAttribute() =>
        Assert.Null(SampleCatalog.FindAttribute("OurOwnMarker"));

    [Theory]
    [InlineData("ViewController")]
    [InlineData("ViewController<DetailView>")]
    [InlineData("DevExpress.ExpressApp.ViewController")]
    [InlineData("ObjectViewController<DetailView, Order>", null)]
    public void FindsControllersThroughGenericsAndNamespaces(string written, string? expected = "ViewController")
    {
        var found = SampleCatalog.FindController(written);

        if (expected is null)
            Assert.Null(found);
        else
            Assert.Equal(expected, found?.Name);
    }

    [Fact]
    public void SurvivesAJsonRoundTrip()
    {
        var restored = XafCatalog.FromJson(SampleCatalog.ToJson());

        Assert.NotNull(restored);
        Assert.Equal("26.1", restored!.DevExpressVersion);
        Assert.Equal(SampleCatalog.TypeCount, restored.TypeCount);
        Assert.Equal(
            "Represents a ViewController descendant that contains the Delete Action.",
            restored.Controllers["DeleteObjectsViewController"].Summary);
    }

    [Fact]
    public void RejectsTextThatIsNotACatalog() =>
        Assert.Null(XafCatalog.FromJson("{ not json"));

    // ------------------------------------------------------------ storage

    [Fact]
    public void SavesAndReadsBackTheNewestCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"xaflogic-catalog-{Guid.NewGuid():N}");

        try
        {
            var path = XafCatalogStore.Save(SampleCatalog, directory);

            Assert.True(File.Exists(path));
            Assert.Single(XafCatalogStore.List(directory));

            var loaded = XafCatalogStore.LoadLatest(directory);
            Assert.Equal("26.1", loaded?.DevExpressVersion);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void AbsentCatalogIsAnOrdinaryOutcome()
    {
        // Most people will never generate one, so this must be silent rather than exceptional.
        var missing = Path.Combine(Path.GetTempPath(), $"xaflogic-none-{Guid.NewGuid():N}");

        Assert.Null(XafCatalogStore.LoadLatest(missing));
        Assert.Empty(XafCatalogStore.List(missing));
    }

    // ----------------------------------------------------------- enriching

    [Fact]
    public void MarksAControllerThatExtendsShippedBehavior()
    {
        var project = ProjectWithController("ArchiveInsteadOfDeleteController", "DeleteObjectsViewController");

        CatalogEnricher.Enrich(project, SampleCatalog);

        var controller = project.Controllers.Single();
        Assert.Equal("DeleteObjectsViewController", controller.FrameworkBaseType);
        Assert.Contains("Delete Action", controller.FrameworkBaseSummary);
        Assert.StartsWith("https://docs.devexpress.com/", controller.FrameworkBaseDocumentationUrl);
    }

    [Theory]
    [InlineData("ViewController")]
    [InlineData("ViewController<DetailView>")]
    public void IgnoresTheGenericControllerBasesEveryControllerUses(string baseType)
    {
        // Every controller derives from one of these, so reporting them listed the whole
        // application under "extends built-in behavior" and said nothing.
        var project = ProjectWithController("ApproveOrderController", baseType);

        CatalogEnricher.Enrich(project, SampleCatalog);

        Assert.Null(project.Controllers.Single().FrameworkBaseType);
    }

    [Fact]
    public void TreatsABaseDefinedInTheSameProjectAsTheTeamsOwn()
    {
        var project = ProjectWithController("InvoiceController", "DeleteObjectsViewController");
        project.Controllers.Add(new ExtractedController
        {
            ClassName = "DeleteObjectsViewController",
            BaseControllerType = "ViewController",
        });

        CatalogEnricher.Enrich(project, SampleCatalog);

        // A project may legitimately define a class with the same name as a framework one; its
        // own layering is not DevExpress behavior.
        Assert.Null(project.Controllers.First().FrameworkBaseType);
    }

    [Fact]
    public void SeparatesTheApplicationsOwnAttributesFromTheFrameworks()
    {
        var project = ProjectWithAttributes("DefaultClassOptions", "Indexed", "AuditedByFinance", "Required");

        CatalogEnricher.Enrich(project, SampleCatalog);

        // Known XAF and known .NET attributes are not the team's own; the invented one is.
        Assert.Equal(["AuditedByFinance"], project.CustomAttributes);
    }

    [Fact]
    public void ReadsAttributeNamesThatCarryArgumentsOrNamespaces()
    {
        var project = ProjectWithAttributes(
            "DevExpress.Xpo.Indexed",
            "OurRule(\"code\", 42)",
            "[DefaultClassOptions]");

        CatalogEnricher.Enrich(project, SampleCatalog);

        Assert.Equal(["OurRule"], project.CustomAttributes);
    }

    [Fact]
    public void RecordsWhichCatalogWasUsed()
    {
        var project = ProjectWithController("AnyController", "ViewController");

        CatalogEnricher.Enrich(project, SampleCatalog);

        Assert.Equal("26.1", project.CatalogVersion);
    }

    [Fact]
    public void DoesNothingWithoutACatalog()
    {
        var project = ProjectWithController("ArchiveController", "DeleteObjectsViewController");

        CatalogEnricher.Enrich(project, null);

        Assert.Null(project.CatalogVersion);
        Assert.Null(project.Controllers.Single().FrameworkBaseType);
        Assert.Empty(project.CustomAttributes);
    }

    [Fact]
    public void ExtractionWithoutACatalogIsUnchanged()
    {
        // The fixtures are extracted with UseCatalog off, which is what every other test relies on.
        Assert.Null(SampleProjects.Xpo.CatalogVersion);
        Assert.Empty(SampleProjects.Xpo.CustomAttributes);
    }

    // ------------------------------------------------------------ helpers

    private static ExtractedProject ProjectWithController(string className, string baseType) =>
        new()
        {
            ProjectName = "Sample",
            Controllers =
            {
                new ExtractedController { ClassName = className, BaseControllerType = baseType },
            },
        };

    private static ExtractedProject ProjectWithAttributes(params string[] attributes) =>
        new()
        {
            ProjectName = "Sample",
            Entities =
            {
                new ExtractedEntity
                {
                    ClassName = "Invoice",
                    Properties =
                    {
                        new ExtractedProperty
                        {
                            Name = "Total",
                            CustomAttributes = [.. attributes],
                        },
                    },
                },
            },
        };
}
