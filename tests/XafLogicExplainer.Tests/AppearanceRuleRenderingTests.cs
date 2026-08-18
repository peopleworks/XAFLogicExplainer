using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Mcp;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Tests;

/// <summary>
/// How an appearance rule reads once it reaches a page, when either half of its opening is absent.
/// </summary>
/// <remarks>
/// Both halves are optional in real code. An identifier is not required — a rule written on a
/// property already says what it governs, and the DevExpress non-persistent-objects demo writes
/// <c>[Appearance("", Enabled = false, TargetItems = "*")]</c> — and a rule may declare no criteria
/// at all, in which case XAF keeps it permanently active.
/// <para>
/// Neither was rendered. The Markdown printed <c>- **** — when ``:</c> — an empty bold span and a
/// condition that looks like it failed to load — and the MCP tool printed the same empty name and
/// simply omitted the condition, which an agent cannot tell from a criteria that was not
/// extracted. <see cref="HtmlExplainerGenerator"/> had already chosen the word <c>always</c>; the
/// other two never received it.
/// </para>
/// <para>
/// These were reachable before rules were read off properties — one unnamed rule per class is
/// enough — but reading them off properties is what made them ordinary.
/// </para>
/// </remarks>
public class AppearanceRuleRenderingTests
{
    private static string Markdown(string language) =>
        string.Join("\n", new MarkdownDocumentationGenerator(language)
            .GenerateSections(SampleProjects.AuditedXpo)
            .Select(section => section.Content))
            .Replace("\r", "");

    [Fact]
    public void NoPageOffersAnEmptyNameWhereARuleShouldBe()
    {
        // The whole defect in one assertion: `****` is not emphasis, it is an identifier that was
        // not there, printed as though it were.
        Assert.DoesNotContain("****", Markdown("en"), StringComparison.Ordinal);
        Assert.DoesNotContain("****", Markdown("es"), StringComparison.Ordinal);
    }

    [Fact]
    public void ARuleWithNoCriteriaSaysItIsAlwaysActive()
    {
        // Not silence, and not an empty code span. In XAF a rule that declares no criteria is
        // permanently in force, which is the stronger claim of the two and the one a reader of a
        // page called "the rules that govern this entity" most needs to see.
        Assert.Contains("- always: enabled=false (fields: ChangedBy)", Markdown("en"), StringComparison.Ordinal);
        Assert.Contains("- always: visibility=Hide (fields: AuditNotes)", Markdown("en"), StringComparison.Ordinal);

        Assert.DoesNotContain("when ``", Markdown("en"), StringComparison.Ordinal);
        Assert.DoesNotContain("cuando ``", Markdown("es"), StringComparison.Ordinal);
    }

    [Fact]
    public void TheWordIsTranslated()
    {
        // It is a rendered word like any other. Left in English it would be the only English word
        // in the Spanish document, on the line that carries the strongest claim on the page.
        Assert.Contains("- siempre: habilitado=false (campos: ChangedBy)", Markdown("es"), StringComparison.Ordinal);
    }

    [Fact]
    public void ANamedRuleWithCriteriaIsUnchanged()
    {
        // The shape the fixtures already had, which must not move: everything above is a fallback
        // and a fallback that captures the ordinary case is a second defect.
        Assert.Contains("- **Audit_ReadOnlyOnceVersioned** — when `RowVersion > 0`: enabled=false",
                        Markdown("en"), StringComparison.Ordinal);
        Assert.Contains("- **Audit_ReadOnlyOnceVersioned** — cuando `RowVersion > 0`: habilitado=false",
                        Markdown("es"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheMcpToolNamesAnUnnamedRuleAndStatesThatItAlwaysApplies()
    {
        var tools = new XafDetailTools(new XafProjectContext(
        [
            new XafProjectSource { Name = "SampleAudited", Path = SampleProjects.AuditedXpoPath, Language = "en" },
        ]));

        var result = await tools.EntityAsync("Receipt", cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain("****", result, StringComparison.Ordinal);
        Assert.Contains("- unnamed rule on ChangedBy", result, StringComparison.Ordinal);
        Assert.Contains("- applies: always", result, StringComparison.Ordinal);

        // And the named one still reads the way it did.
        Assert.Contains("- **Audit_ReadOnlyOnceVersioned** on", result, StringComparison.Ordinal);
        Assert.Contains("- when: `RowVersion > 0`", result, StringComparison.Ordinal);
    }
}
