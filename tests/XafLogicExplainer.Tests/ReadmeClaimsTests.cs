using System.Text.RegularExpressions;

namespace XafLogicExplainer.Tests;

/// <summary>
/// Keeps the counts the README states true.
/// </summary>
/// <remarks>
/// The Status section names a version, a tool count and a test count. All three drift the moment
/// anything is added, and nothing about a stale number makes a build fail — it is simply wrong on
/// the front page of the repository, in the NuGet listing, and on every MCP directory that reads
/// the README to describe the project.
/// <para>
/// The same argument the tool makes about generated documentation: a closed-world statement is
/// only useful if it is true, and the way to keep it true is to check it rather than to remember.
/// </para>
/// </remarks>
public class ReadmeClaimsTests
{
    [Fact]
    public void StatesTheVersionTheBuildActuallyIs()
    {
        var claimed = Claim(@"\*\*v(\d+\.\d+\.\d+)\.\*\*");
        var actual = typeof(Core.Analyzers.LogicExtractor).Assembly
            .GetName().Version!;

        Assert.Equal($"{actual.Major}.{actual.Minor}.{actual.Build}", claimed);
    }

    [Fact]
    public void CountsTheToolsTheMcpServerExposes()
    {
        var claimed = int.Parse(Claim(@"\*\*MCP server\*\* — (\d+) tools"));

        // Counted from the source rather than from a list kept here, which would be one more thing
        // to forget.
        var declared = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "XafLogicExplainer.Mcp"), "*.cs",
                SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"McpServerTool\(Name = ""(\w+)"""))
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Count();

        Assert.Equal(declared, claimed);
    }

    [Fact]
    public void CountsTheTestsThatActuallyExist()
    {
        var claimed = int.Parse(Claim(@"\*\*(\d+) tests\*\*"));

        // Cases, not methods. A reader takes "176 tests" to mean what the runner reports, and a
        // [Theory] contributes one test per [InlineData] rather than one for the method — counting
        // methods here understated the suite by thirty.
        var cases = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Fixtures{Path.DirectorySeparatorChar}"))
            .Sum(file =>
            {
                var source = File.ReadAllText(file);
                return Regex.Matches(source, @"^\s*\[Fact[\]\(]", RegexOptions.Multiline).Count
                     + Regex.Matches(source, @"^\s*\[InlineData\(", RegexOptions.Multiline).Count;
            });

        Assert.Equal(cases, claimed);
    }

    private static string Claim(string pattern)
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot, "README.md"));
        var match = Regex.Match(readme, pattern);

        Assert.True(match.Success, $"The README no longer contains a claim matching /{pattern}/. " +
                                   "If the Status section was rewritten, update this test with it.");
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Walks up from the test assembly to the working copy, the way the fixtures are found.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "README.md")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                $"No README.md found above '{AppContext.BaseDirectory}'.");
        }
    }
}
