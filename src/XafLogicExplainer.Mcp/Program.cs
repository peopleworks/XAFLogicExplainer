using XafLogicExplainer.Mcp;

// ============================================================
// XAF Logic Explainer — MCP server
//
// Runs standalone, so an MCP client can launch it with `dnx XafLogicExplainer.Mcp` without
// installing the CLI. The CLI hosts the same server as `xaflogic mcp`; this file only supplies
// an entry point for the package.
//
// stdout is the JSON-RPC channel. Nothing here may write to it -- every message goes to stderr.
// ============================================================

var projectPath = GetArgument("--project") ?? Environment.GetEnvironmentVariable("XAFLOGIC_PROJECT");
var language = GetArgument("--lang") ?? "en";
var orm = GetArgument("--orm");

if (args.Contains("--help") || args.Contains("-h"))
{
    await Console.Error.WriteLineAsync("""
        XAF Logic Explainer — Model Context Protocol server

        Answers questions about a specific DevExpress XAF application by reading its source.

        Usage:
          xaflogic-mcp [--project <module directory>] [--lang en|es] [--orm auto|xpo|efcore]

        With no --project, the XAF module is looked for at or below the working directory, which
        is what lets an MCP client launch this with no configuration from a solution folder.

        Environment:
          XAFLOGIC_PROJECT   same as --project
        """);
    return 0;
}

projectPath ??= XafModuleLocator.Locate(Directory.GetCurrentDirectory());

if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
{
    await Console.Error.WriteLineAsync(
        "xaflogic-mcp: no XAF project found. Pass --project <module directory>, set " +
        "XAFLOGIC_PROJECT, or start the server from a directory containing an XAF module.");
    return 1;
}

await Console.Error.WriteLineAsync($"xaflogic-mcp: reading {projectPath}");

await McpServerRunner.RunStdioAsync(
[
    new XafProjectSource
    {
        Name = new DirectoryInfo(projectPath).Name,
        Path = projectPath,
        Language = language,
        Orm = orm,
    },
]);

return 0;

string? GetArgument(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
