using System.Text.RegularExpressions;
using XafLogicExplainer.Core.Models;
using XafLogicExplainer.Mcp;
using XafLogicExplainer.Mcp.Tools;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That a citation lands on the line it claims to.
/// </summary>
/// <remarks>
/// The extraction knew which <em>file</em> a class was in and nothing narrower, so an agent told
/// about a controller still had to search the file for it, and a walkthrough could not cite a step
/// at all — every claim in one is supposed to carry a <c>file:line</c> a reader can open.
/// <para>
/// These tests read the fixture back off disk and check that the cited line really contains the
/// declaration. That is the only assertion that catches the two ways this silently goes wrong: an
/// off-by-one from a zero-based line, and a span that starts at the first attribute rather than at
/// the name — <c>Customer</c> carries four attributes and a doc comment, so the two answers are
/// four lines apart.
/// </para>
/// </remarks>
public class DeclarationLocationTests
{
    private static readonly (string Name, ExtractedProject Project)[] Samples =
    [
        ("Xpo", SampleProjects.Xpo),
        ("EfCore", SampleProjects.EfCore),
        ("LegacyEf", SampleProjects.LegacyEf),
        ("PocoEf", SampleProjects.PocoEf),
        ("DeepXpo", SampleProjects.DeepXpo),
        ("AuditedXpo", SampleProjects.AuditedXpo),
        ("Demo", SampleProjects.Demo),
    ];

    /// <summary>The text of the cited line, or a failure that says which citation was wrong.</summary>
    private static string LineAt(string what, string filePath, int line)
    {
        Assert.True(line > 0, $"{what} carries no line.");
        Assert.True(File.Exists(filePath), $"{what} cites a file that is not there: {filePath}");

        var lines = File.ReadAllLines(filePath);

        Assert.True(line <= lines.Length,
            $"{what} cites line {line} of {Path.GetFileName(filePath)}, which has {lines.Length}.");

        return lines[line - 1];
    }

    private static void AssertDeclares(string what, string name, string filePath, int line)
    {
        var text = LineAt(what, filePath, line);

        Assert.True(
            Regex.IsMatch(text, $@"\b(class|record|interface|struct)\s+{Regex.Escape(name)}\b"),
            $"{what} cites {Path.GetFileName(filePath)}:{line}, which reads: {text.Trim()}");
    }

    [Fact]
    public void EveryEntityIsCitedAtTheLineItsNameIsOn()
    {
        foreach (var (sample, project) in Samples)
        {
            foreach (var entity in project.Entities)
                AssertDeclares($"{sample}/{entity.ClassName}", entity.ClassName, entity.FilePath, entity.Line);
        }
    }

    [Fact]
    public void EveryControllerIsCitedAtTheLineItsNameIsOn()
    {
        foreach (var (sample, project) in Samples)
        {
            foreach (var controller in project.Controllers)
            {
                AssertDeclares($"{sample}/{controller.ClassName}",
                    controller.ClassName, controller.FilePath, controller.Line);
            }
        }
    }

    [Fact]
    public void AttributesAboveAClassDoNotPullTheCitationUpToThem()
    {
        // The case the identifier token exists to get right. `Customer` sits behind a doc comment
        // and four attributes, so a citation taken from the declaration's span would name
        // `[DefaultClassOptions]` — correct about the syntax, and four lines from what the reader
        // is looking for.
        var customer = SampleProjects.Xpo.Entity("Customer");

        Assert.Contains("class Customer", LineAt("Customer", customer.FilePath, customer.Line),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnActionIsCitedWhereItIsDeclaredRatherThanWhereItsControllerIs()
    {
        var controller = SampleProjects.Xpo.Controllers
            .First(c => c.ClassName == "ApproveOrderController");

        var action = Assert.Single(controller.Actions);

        // The field, not the class: an action's own declaration is the line somebody editing it
        // opens, and on a partial controller it need not even be in the controller's file.
        Assert.Contains("_approveAction", LineAt("ApproveOrder", action.FilePath, action.Line),
            StringComparison.Ordinal);

        Assert.NotEqual(controller.Line, action.Line);
    }

    [Fact]
    public void AMethodIsCitedAtItsOwnName()
    {
        var controller = SampleProjects.Xpo.Controllers
            .First(c => c.ClassName == "ApproveOrderController");

        var method = controller.Methods.First(m => m.Name == "ApproveAction_Execute");

        Assert.Contains("ApproveAction_Execute", LineAt("ApproveAction_Execute", method.FilePath, method.Line),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAgentIsToldWhereToOpenTheFile()
    {
        // The half that reaches a consumer. The tools named an entity and a controller and never
        // said where either one was, so an agent's next move was to search for what it had just
        // been handed.
        var tools = new XafDetailTools(new XafProjectContext(
            [new XafProjectSource { Name = "SampleApp", Path = SampleProjects.XpoPath, Language = "en" }]));

        var entity = await tools.EntityAsync("Customer", cancellationToken: TestContext.Current.CancellationToken);
        var controller = await tools.ControllerAsync(
            "ApproveOrderController", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("Declared at ", entity, StringComparison.Ordinal);
        Assert.Contains("Customer.cs:", entity, StringComparison.Ordinal);

        Assert.Contains("Declared at ", controller, StringComparison.Ordinal);
        Assert.Contains("ApproveOrderController.cs:", controller, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePathIsSaidOnceAndTheMembersSayOnlyTheirLine()
    {
        // Found by reading the response rather than the assertions: the first version repeated a
        // hundred characters of absolute path on every action and every helper method. A controller
        // with eight actions would have spent a paragraph of an agent's context on one file name.
        var tools = new XafDetailTools(new XafProjectContext(
            [new XafProjectSource { Name = "SampleApp", Path = SampleProjects.XpoPath, Language = "en" }]));

        var controller = await tools.ControllerAsync(
            "ApproveOrderController", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("- Declared at line 13", controller, StringComparison.Ordinal);
        Assert.Contains("Declared at line 31.", controller, StringComparison.Ordinal);

        var occurrences = controller.Split("ApproveOrderController.cs").Length - 1;

        Assert.True(occurrences == 1,
            $"The file is named {occurrences} times; once is enough for every member declared in it.");
    }
}
