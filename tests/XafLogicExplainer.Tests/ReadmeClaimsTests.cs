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
    public void StatesTheSameVersionEverywhereItIsWritten()
    {
        var assembly = typeof(Core.Analyzers.LogicExtractor).Assembly.GetName().Version!;
        var actual = $"{assembly.Major}.{assembly.Minor}.{assembly.Build}";

        Assert.Equal(actual, Claim(@"\*\*v(\d+\.\d+\.\d+)\.\*\*"));

        // The plugin manifest and the skill carry their own version, and neither is built, packed
        // or released — nothing has ever forced them to move. Both sat at 0.9.0 through two
        // releases, telling anyone who installed the plugin they had a version that no longer
        // existed.
        //
        // Every occurrence, not the first. `server.json` writes the version twice: once for the
        // server and once for the NuGet package the registry resolves it to. Checking only the
        // first passes a release whose registry entry advertises a package version that was never
        // published — and it is the second one, further down a file nobody rereads.
        foreach (var (file, pattern) in Manifests)
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot, file));
            var matches = Regex.Matches(text, pattern);

            Assert.True(matches.Count > 0, $"No version found in {file}.");

            foreach (var match in matches.Cast<Match>())
                Assert.Equal(actual, match.Groups[1].Value);
        }
    }

    private static (string File, string Pattern)[] Manifests =>
    [
        ("plugins/xaf-logic-explainer/.claude-plugin/plugin.json", @"""version"": ""(\d+\.\d+\.\d+)"""),
        ("plugins/xaf-logic-explainer/skills/xaf-application-knowledge/SKILL.md", @"version: ""(\d+\.\d+\.\d+)"""),
        ("src/XafLogicExplainer.Mcp/.mcp/server.json", @"""version"": ""(\d+\.\d+\.\d+)"""),
    ];

    [Theory]
    [InlineData("src/XafLogicExplainer.Mcp/.mcp/server.json")]
    [InlineData("plugins/xaf-logic-explainer/.claude-plugin/plugin.json")]
    [InlineData(".claude-plugin/marketplace.json")]
    public void ShipsManifestsWithoutAByteOrderMark(string file)
    {
        // nuget.org rejects the package outright — "The MCP server metadata file '.mcp/server.json'
        // is invalid" — and the byte order mark is invisible in every editor and every diff, so the
        // failure arrives at publish time with nothing to look at.
        //
        // Added after exactly that: a bulk version bump written with Python's utf-8-sig, which
        // preserves a BOM where one exists and adds one where it does not.
        var bytes = File.ReadAllBytes(Path.Combine(RepositoryRoot, file));

        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            $"{file} starts with a UTF-8 byte order mark. Strict JSON readers reject it, " +
            "including the one nuget.org runs when it validates an MCP package.");
    }

    [Fact]
    public void CountsTheToolsTheMcpServerExposes()
    {
        var claimed = int.Parse(Claim(@"\*\*MCP server\*\* — (\d+) tools"));

        Assert.Equal(DeclaredTools.Count, claimed);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("src/XafLogicExplainer.Mcp/README.md")]
    public void DescribesEveryToolItShips(string file)
    {
        // The count alone was not enough: the table a reader actually consults to decide whether
        // to install listed seven of nine, and the two it omitted are the ones nothing else
        // extracts.
        //
        // Both READMEs, because fixing the front page left the packed one wrong -- and the packed
        // one is what nuget.org shows and what the MCP directories import.
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot, file));
        var undocumented = DeclaredTools.Where(tool => !readme.Contains($"`{tool}`")).ToList();

        Assert.True(undocumented.Count == 0,
            $"The MCP server exposes tools {file} never mentions: {string.Join(", ", undocumented)}.");
    }

    /// <summary>
    /// The tool names, read from the server's source rather than from a list kept here — which
    /// would be one more thing to forget.
    /// </summary>
    private static IReadOnlyCollection<string> DeclaredTools => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "XafLogicExplainer.Mcp"), "*.cs",
            SearchOption.AllDirectories)
        .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"McpServerTool\(Name = ""(\w+)"""))
        .Select(match => match.Groups[1].Value)
        .Distinct()
        .ToList();

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
