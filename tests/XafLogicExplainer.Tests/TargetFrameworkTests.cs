using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Generators;

namespace XafLogicExplainer.Tests;

/// <summary>
/// That an application is asked what framework it targets in the spelling it actually uses.
/// </summary>
/// <remarks>
/// A project from before the SDK format declares its framework in an element the modern format
/// does not have. Reading only the modern one returns nothing for it — and nothing is not a
/// smaller answer than <c>net48</c>, it is a different one. An agent told nothing assumes a modern
/// framework and writes code that will not build, which is the failure this is here to stop.
/// <para>
/// The assertions run through to the generated <c>AGENTS.md</c> rather than stopping at the
/// extraction result, because the extraction being right is not the same as the agent being told.
/// </para>
/// </remarks>
public class TargetFrameworkTests
{
    // ------------------------------------------------------------ the three spellings

    [Fact]
    public void ReadsTheSdkSpelling()
    {
        Assert.Equal("net9.0", DeclaredTargetFramework.FromProjectFile(
            "<Project><PropertyGroup><TargetFramework>net9.0</TargetFramework></PropertyGroup></Project>"));
    }

    /// <summary>
    /// A multi-targeting project is reported as it declared itself, list and all.
    /// </summary>
    /// <remarks>
    /// Picking one of the list would be inventing a fact. The list is also what an agent needs:
    /// code has to compile on every framework named, so the oldest is the real constraint.
    /// </remarks>
    [Fact]
    public void ReadsAMultiTargetingProjectAsTheListItDeclares()
    {
        Assert.Equal("net8.0;net9.0", DeclaredTargetFramework.FromProjectFile(
            "<Project><PropertyGroup><TargetFrameworks>net8.0;net9.0</TargetFrameworks></PropertyGroup></Project>"));
    }

    [Theory]
    [InlineData("v4.8", "net48")]
    [InlineData("v4.8.1", "net481")]
    [InlineData("v4.5.2", "net452")]
    [InlineData("v3.5", "net35")]
    public void ReadsThePreSdkSpellingAndNormalisesIt(string declared, string expected)
    {
        Assert.Equal(expected, DeclaredTargetFramework.FromProjectFile(
            $"<Project><PropertyGroup><TargetFrameworkVersion>{declared}</TargetFrameworkVersion></PropertyGroup></Project>"));
    }

    /// <summary>
    /// A project that declares no framework in any spelling declares none, and says so as null.
    /// </summary>
    /// <remarks>
    /// It has to stay distinct from a value rather than collapsing into something that reads like
    /// a framework nobody named. A module inheriting its framework from a shared
    /// <c>Directory.Build.props</c> is the ordinary case for this.
    /// </remarks>
    [Fact]
    public void AProjectDeclaringNoFrameworkReturnsNull()
    {
        Assert.Null(DeclaredTargetFramework.FromProjectFile(
            "<Project><PropertyGroup><OutputType>Library</OutputType></PropertyGroup></Project>"));
    }

    // ------------------------------------------------------ which monikers forbid modern C#

    [Theory]
    [InlineData("net48", true)]
    [InlineData("net481", true)]
    [InlineData("net472", true)]
    [InlineData("net35", true)]
    [InlineData("net9.0", false)]
    [InlineData("net8.0", false)]
    [InlineData("netstandard2.0", false)]
    [InlineData("", false)]
    public void KnowsWhichMonikersAreDotNetFramework(string moniker, bool expected)
    {
        Assert.Equal(expected, DeclaredTargetFramework.IsDotNetFramework(moniker));
    }

    /// <summary>
    /// <c>net10.0</c> is .NET 10, not .NET Framework 1.0 with a stray dot.
    /// </summary>
    /// <remarks>
    /// It is the one moniker where a rule matching "net" followed by a low digit gets the answer
    /// exactly backwards, and it is the framework this tool itself is built on — so it would have
    /// been wrong about its own repository first.
    /// </remarks>
    [Fact]
    public void DoesNotMistakeNet10ForDotNetFramework()
    {
        Assert.False(DeclaredTargetFramework.IsDotNetFramework("net10.0"));
    }

    // ------------------------------------------------------------- through to the document

    [Fact]
    public void ALegacyProjectReportsItsFramework()
    {
        Assert.Equal("net48", SampleProjects.LegacyFramework.TargetFramework);
    }

    /// <summary>
    /// The pre-SDK format is read for its DevExpress version too, from assembly references.
    /// </summary>
    /// <remarks>
    /// Pinned here because the two readings share a project file and a fixture: a change that
    /// broke one while fixing the other would otherwise look green.
    /// </remarks>
    [Fact]
    public void ALegacyProjectStillReportsItsDevExpressVersion()
    {
        Assert.Equal("22.1", SampleProjects.LegacyFramework.DeclaredDevExpressVersion);
    }

    /// <summary>
    /// Old-shaped C# is extracted as readily as the modern shape.
    /// </summary>
    /// <remarks>
    /// Block namespaces and backing-field properties are what compiles under the C# version this
    /// project gets by default. A fixture that declared a legacy framework but wrote modern C#
    /// would prove the project file was read and nothing else.
    /// </remarks>
    [Fact]
    public void ExtractsEntitiesWrittenInTheOldShape()
    {
        var contrato = SampleProjects.LegacyFramework.Entity("Contrato");

        Assert.Equal(["Numero", "Monto"], contrato.Properties.Select(p => p.Name));
    }

    [Fact]
    public void TheAgentDocumentNamesTheFramework()
    {
        var agents = new AgentContextGenerator("0.0.0-test")
            .GenerateIndex(SampleProjects.LegacyFramework, []);

        Assert.Contains("targeting **net48**", agents, StringComparison.Ordinal);
    }

    /// <summary>
    /// And carries it as a rule, because naming a framework in a summary is not the same as
    /// forbidding what it forbids.
    /// </summary>
    [Fact]
    public void TheAgentDocumentCarriesADotNetFrameworkGroundRule()
    {
        var agents = new AgentContextGenerator("0.0.0-test")
            .GenerateIndex(SampleProjects.LegacyFramework, []);

        Assert.Contains(".NET Framework application (`net48`)", agents, StringComparison.Ordinal);
        Assert.Contains("default interface methods cannot work at all", agents, StringComparison.Ordinal);
    }

    /// <summary>
    /// A modern application is never handed that rule.
    /// </summary>
    /// <remarks>
    /// The same discipline the ORM rule keeps: a constraint stated where it does not apply is
    /// worse than no constraint, because the reader cannot tell it from one that was read.
    /// </remarks>
    [Fact]
    public void AModernApplicationGetsNoDotNetFrameworkRule()
    {
        var agents = new AgentContextGenerator("0.0.0-test").GenerateIndex(SampleProjects.Xpo, []);

        Assert.DoesNotContain(".NET Framework application", agents, StringComparison.Ordinal);
    }

    /// <summary>
    /// A project whose framework was never declared renders no field at all, not an empty one.
    /// </summary>
    /// <remarks>
    /// A framework label with nothing after it is not the honest version of "not declared"; it
    /// reads as a document that lost a value, and it appeared on every page generated for a
    /// module with no project file.
    /// </remarks>
    [Fact]
    public void AnUndeclaredFrameworkRendersNoEmptyField()
    {
        Assert.Equal(string.Empty, SampleProjects.PocoEf.TargetFramework);

        var markdown = new MarkdownDocumentationGenerator().GenerateMarkdown(SampleProjects.PocoEf);

        Assert.DoesNotContain("*Framework:", markdown, StringComparison.Ordinal);
    }
}
