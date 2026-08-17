using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using Spectre.Console;
using XafLogicExplainer.Cli.Helpers;
using XafLogicExplainer.Cli.Models;
using XafLogicExplainer.CopilotSync.Models;
using XafLogicExplainer.CopilotSync.Services;
using XafLogicExplainer.Core.Analyzers;
using XafLogicExplainer.Core.Diff;
using XafLogicExplainer.Core.Generators;
using XafLogicExplainer.Core.Hashing;
using XafLogicExplainer.Core.Sinks;
using XafLogicExplainer.Mcp;
using XafLogicExplainer.Core.Catalog;
using XafLogicExplainer.DxCatalog;
using Microsoft.Extensions.AI;
using OpenAI;
using XafLogicExplainer.Core.Models;

// ============================================================
// xaflogic - XAF Logic Explainer CLI
// ============================================================

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    AnsiConsole.Write(new FigletText("XAF Logic").Color(Color.Blue));
    AnsiConsole.MarkupLine("[grey]Teach your AI coding agent what your XAF application actually does[/]");
    AnsiConsole.WriteLine();
}

// ============================================================
// SHARED OPTIONS (reused across commands)
// ============================================================

var apiUrlOption = new Option<string?>("--api-url", "PeopleWorks Copilot API URL");
var tokenOption = new Option<string?>("--token", "PeopleWorks Copilot API token");
var userNameOption = new Option<string?>("--user-name", "Copilot user name");
var resourceNameOption = new Option<string?>("--resource-name", "Copilot resource name");
var projectPathOption = new Option<string?>("--project", "XAF module directory path");
projectPathOption.AddAlias("-p");
var languageOption = new Option<string?>("--lang", "Language for documentation (es/en)");
var forceOption = new Option<bool>("--force", "Force operation even if no changes detected");
var ormOption = new Option<string?>("--orm", "ORM type: auto, xpo, efcore (default: auto)");
var allOption = new Option<bool>("--all", "Process all configured projects");
allOption.AddAlias("-a");
var enrichOption = new Option<bool>("--enrich", "Enrich controllers with AI-generated business logic summaries");

// ============================================================
// ROOT COMMAND
// ============================================================

var rootCommand = new RootCommand("XAF Logic Explainer CLI - Extract and sync XAF project documentation");

// ============================================================
// COMMAND: config
// ============================================================

var configCommand = new Command("config", "Configure default settings (stored in ~/.xaflogic/config.json)");
var configApiUrlOption = new Option<string?>("--api-url", "PeopleWorks Copilot API URL");
var configTokenOption = new Option<string?>("--token", "Authentication token");
var configUserNameOption = new Option<string?>("--user-name", "Default user name");
var configResourceNameOption = new Option<string?>("--resource-name", "Default resource name");
var configProjectPathOption = new Option<string?>("--project", "Default XAF module directory path");
configProjectPathOption.AddAlias("-p");
var configLanguageOption = new Option<string?>("--lang", "Default language (es/en)");
var configClearOption = new Option<bool>("--clear", "Clear all saved configuration");

configCommand.AddOption(configApiUrlOption);
configCommand.AddOption(configTokenOption);
configCommand.AddOption(configUserNameOption);
configCommand.AddOption(configResourceNameOption);
configCommand.AddOption(configProjectPathOption);
configCommand.AddOption(configLanguageOption);
configCommand.AddOption(configClearOption);

configCommand.SetHandler((apiUrl, token, userName, resourceName, projectPath, language, clear) =>
{
    if (clear)
    {
        ConfigHelper.Clear();
        AnsiConsole.MarkupLine("[green]✓[/] Configuration cleared");
        return;
    }

    var config = ConfigHelper.Load();
    var changed = false;

    if (apiUrl != null) { config.ApiUrl = apiUrl; changed = true; }
    if (token != null) { config.Token = token; changed = true; }
    if (userName != null) { config.UserName = userName; changed = true; }
    if (resourceName != null) { config.ResourceName = resourceName; changed = true; }
    if (projectPath != null) { config.ProjectPath = Path.GetFullPath(projectPath); changed = true; }
    if (language != null) { config.Language = language; changed = true; }

    if (changed)
    {
        ConfigHelper.Save(config);
        AnsiConsole.MarkupLine("[green]✓[/] Configuration saved");
    }

    // Display current config
    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Setting");
    table.AddColumn("Value");

    table.AddRow("Config file", ConfigHelper.GetConfigPath());
    table.AddRow("API URL", config.ApiUrl ?? "[grey](not set)[/]");
    table.AddRow("Token", config.Token != null ? config.Token[..Math.Min(10, config.Token.Length)] + "..." : "[grey](not set)[/]");
    table.AddRow("User Name", config.UserName ?? "[grey](not set)[/]");
    table.AddRow("Resource Name", config.ResourceName ?? "[grey](not set)[/]");
    table.AddRow("Project Path", config.ProjectPath ?? "[grey](not set)[/]");
    table.AddRow("Language", config.Language ?? "[grey](not set)[/]");

    AnsiConsole.Write(table);

    // Show projects if any
    if (config.Projects.Count > 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[blue]Projects:[/] {config.Projects.Count} configured");
        var projectsTable = new Table().Border(TableBorder.Simple);
        projectsTable.AddColumn("#");
        projectsTable.AddColumn("Name");
        projectsTable.AddColumn("Resource");
        projectsTable.AddColumn("Language");
        projectsTable.AddColumn("ORM");
        for (int i = 0; i < config.Projects.Count; i++)
        {
            var p = config.Projects[i];
            projectsTable.AddRow(
                (i + 1).ToString(),
                p.Name,
                p.ResourceName,
                p.Language ?? "[grey](global)[/]",
                p.Orm ?? "[grey](auto)[/]");
        }
        AnsiConsole.Write(projectsTable);
    }

}, configApiUrlOption, configTokenOption, configUserNameOption, configResourceNameOption,
   configProjectPathOption, configLanguageOption, configClearOption);

rootCommand.AddCommand(configCommand);

// ============================================================
// COMMAND: projects (list / add / remove)
// ============================================================

var projectsCommand = new Command("projects", "Manage multi-project configuration");

// Default: list projects
projectsCommand.SetHandler(() =>
{
    var config = ConfigHelper.Load();
    if (config.Projects.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No projects configured. Add one with: [cyan]xaflogic projects add --name <name> --project <path> --resource-name <resource>[/]");
        return;
    }

    var table = new Table().Border(TableBorder.Rounded).Title("[blue]Configured Projects[/]");
    table.AddColumn("#");
    table.AddColumn("Name");
    table.AddColumn("Path");
    table.AddColumn("Resource");
    table.AddColumn("Language");
    table.AddColumn("ORM");

    for (int i = 0; i < config.Projects.Count; i++)
    {
        var p = config.Projects[i];
        var pathExists = Directory.Exists(p.ProjectPath) ? "[green]OK[/]" : "[red]NOT FOUND[/]";
        table.AddRow(
            (i + 1).ToString(),
            p.Name,
            $"{p.ProjectPath} ({pathExists})",
            p.ResourceName,
            p.Language ?? "[grey](global)[/]",
            p.Orm ?? "[grey](auto)[/]");
    }
    AnsiConsole.Write(table);
});

// Subcommand: add
var addProjectCommand = new Command("add", "Add a project to the configuration");
var addNameOption = new Option<string>("--name", "Project friendly name") { IsRequired = true };
var addProjectPathOption = new Option<string>("--project", "XAF module directory path") { IsRequired = true };
addProjectPathOption.AddAlias("-p");
var addResourceOption = new Option<string>("--resource-name", "Copilot resource name") { IsRequired = true };
var addLangOption = new Option<string?>("--lang", "Language override (es/en)");
var addOrmOption = new Option<string?>("--orm", "ORM override (auto/xpo/efcore)");

addProjectCommand.AddOption(addNameOption);
addProjectCommand.AddOption(addProjectPathOption);
addProjectCommand.AddOption(addResourceOption);
addProjectCommand.AddOption(addLangOption);
addProjectCommand.AddOption(addOrmOption);

addProjectCommand.SetHandler((name, path, resource, lang, orm) =>
{
    var fullPath = Path.GetFullPath(path);
    if (!Directory.Exists(fullPath))
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Directory not found: {fullPath}");
        return;
    }

    var config = ConfigHelper.Load();
    if (config.Projects.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Project '{name}' already exists. Remove it first or use a different name.");
        return;
    }

    config.Projects.Add(new ProjectConfig
    {
        Name = name,
        ProjectPath = fullPath,
        ResourceName = resource,
        Language = lang,
        Orm = orm
    });

    ConfigHelper.Save(config);
    AnsiConsole.MarkupLine($"[green]✓[/] Project '{name}' added ({config.Projects.Count} total)");

}, addNameOption, addProjectPathOption, addResourceOption, addLangOption, addOrmOption);

projectsCommand.AddCommand(addProjectCommand);

// Subcommand: remove
var removeProjectCommand = new Command("remove", "Remove a project from the configuration");
var removeNameOption = new Option<string>("--name", "Project name to remove") { IsRequired = true };
removeProjectCommand.AddOption(removeNameOption);

removeProjectCommand.SetHandler((name) =>
{
    var config = ConfigHelper.Load();
    var removed = config.Projects.RemoveAll(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    if (removed == 0)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Project '{name}' not found.");
        return;
    }

    ConfigHelper.Save(config);
    AnsiConsole.MarkupLine($"[green]✓[/] Project '{name}' removed ({config.Projects.Count} remaining)");

}, removeNameOption);

projectsCommand.AddCommand(removeProjectCommand);
rootCommand.AddCommand(projectsCommand);

// ============================================================
// COMMAND: extract
// ============================================================

var extractCommand = new Command("extract", "Extract documentation from XAF project (local output)");
extractCommand.AddOption(projectPathOption);
extractCommand.AddOption(languageOption);
extractCommand.AddOption(forceOption);
extractCommand.AddOption(ormOption);
extractCommand.AddOption(allOption);
extractCommand.AddOption(enrichOption);

extractCommand.SetHandler(async (projectPath, language, force, orm, all, enrich) =>
{
    var config = ConfigHelper.Load();

    // --all: batch extract all configured projects
    if (all)
    {
        if (config.Projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No projects configured. Add one with: [cyan]xaflogic projects add[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Extracting {config.Projects.Count} projects...[/]");
        AnsiConsole.WriteLine();

        var summary = new Table().Border(TableBorder.Rounded).Title("[blue]Multi-Project Extraction[/]");
        summary.AddColumn("Project");
        summary.AddColumn("ORM");
        summary.AddColumn("Entities");
        summary.AddColumn("Controllers");
        summary.AddColumn("Status");

        foreach (var proj in config.Projects)
        {
            var projLang = proj.Language ?? config.Language ?? "es";
            var projOrm = proj.Orm ?? config.Orm;

            if (!Directory.Exists(proj.ProjectPath))
            {
                summary.AddRow(proj.Name, "[grey]?[/]", "-", "-", "[red]PATH NOT FOUND[/]");
                continue;
            }

            var projHashCalc = new ProjectHashCalculator();
            if (!force && !projHashCalc.HasChanged(proj.ProjectPath))
            {
                summary.AddRow(proj.Name, "[grey]?[/]", "-", "-", "[yellow]NO CHANGES[/]");
                continue;
            }

            try
            {
                var extractor = new LogicExtractor();
                var projOptions = BuildExtractionOptions(projLang, projOrm);
                var extracted = extractor.ExtractFromSourceDirectory(proj.ProjectPath, projOptions);

                // Generate docs
                var projGenerator = new MarkdownDocumentationGenerator(projLang);
                var projOutputDir = Path.Combine(proj.ProjectPath, ".xaflogic-output");
                Directory.CreateDirectory(projOutputDir);

                var projSchemaPath = Path.Combine(projOutputDir, $"{extracted.ProjectName}_Schema.json");
                var projPreviousPath = Path.Combine(projOutputDir, $"{extracted.ProjectName}_Previous.json");
                if (File.Exists(projSchemaPath))
                    File.Copy(projSchemaPath, projPreviousPath, overwrite: true);

                var projSections = projGenerator.GenerateSections(extracted);
                foreach (var section in projSections)
                    File.WriteAllText(Path.Combine(projOutputDir, $"{section.FileName}.md"), section.Content);

                var projFullMd = projGenerator.GenerateMarkdown(extracted);
                File.WriteAllText(Path.Combine(projOutputDir, $"{extracted.ProjectName}_Full.md"), projFullMd);

                var projJson = projGenerator.GenerateJson(extracted);
                File.WriteAllText(projSchemaPath, projJson);

                projHashCalc.SaveHash(proj.ProjectPath, extracted.SourceHash);

                summary.AddRow(proj.Name, extracted.OrmType,
                    extracted.Entities.Count.ToString(),
                    extracted.Controllers.Count.ToString(),
                    "[green]OK[/]");
            }
            catch (Exception ex)
            {
                summary.AddRow(proj.Name, "[grey]?[/]", "-", "-", $"[red]{Markup.Escape(ex.Message[..Math.Min(50, ex.Message.Length)])}[/]");
            }
        }

        AnsiConsole.Write(summary);
        AnsiConsole.MarkupLine("[green]✓[/] Batch extraction complete");
        return;
    }

    projectPath ??= config.ProjectPath;
    language ??= config.Language ?? "es";
    orm ??= config.Orm;

    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] --project is required. Set it with: xaflogic config --project <path>");
        return;
    }

    AnsiConsole.MarkupLine($"[blue]Project:[/] {projectPath}");

    var hashCalc = new ProjectHashCalculator();
    if (!force && !hashCalc.HasChanged(projectPath))
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No changes detected. Use --force to re-extract.");
        return;
    }

    ExtractedProject project = null!;
    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Extracting project logic...", async ctx =>
    {
        var extractor = new LogicExtractor();
        var options = BuildExtractionOptions(language, orm);
        project = extractor.ExtractFromSourceDirectory(projectPath, options);
        await Task.CompletedTask;
    });

    if (enrich)
    {
        await EnrichWithAi(project, config, language!);
    }

    // Summary table
    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Metric");
    table.AddColumn("Count");
    table.AddRow("ORM", project.OrmType);
    table.AddRow("Entities", project.Entities.Count.ToString());
    table.AddRow("Controllers", project.Controllers.Count.ToString());
    table.AddRow("Navigation groups", project.Navigation.Count.ToString());
    table.AddRow("Seed data methods", project.SeedData.Count.ToString());
    if (project.ModelEditorInfo != null)
    {
        table.AddRow("Model classes (xafml)", project.ModelEditorInfo.BOModelClasses.Count.ToString());
        table.AddRow("Model views (xafml)", project.ModelEditorInfo.Views.Count.ToString());
        table.AddRow("Schema modules", project.ModelEditorInfo.SchemaModules.Count.ToString());
        table.AddRow("XAFML files", project.ModelEditorInfo.SourceFiles.Count.ToString());
    }
    table.AddRow("Hash", project.SourceHash[..16] + "...");
    AnsiConsole.Write(table);

    // Generate docs
    var generator = new MarkdownDocumentationGenerator(language!);
    var outputDir = Path.Combine(projectPath, ".xaflogic-output");
    Directory.CreateDirectory(outputDir);

    // Auto-save previous snapshot for diff
    var schemaPath = Path.Combine(outputDir, $"{project.ProjectName}_Schema.json");
    var previousPath = Path.Combine(outputDir, $"{project.ProjectName}_Previous.json");
    if (File.Exists(schemaPath))
        File.Copy(schemaPath, previousPath, overwrite: true);

    var sections = generator.GenerateSections(project);
    var docsTable = new Table().Border(TableBorder.Rounded);
    docsTable.AddColumn("Section");
    docsTable.AddColumn("Size");

    foreach (var section in sections)
    {
        File.WriteAllText(Path.Combine(outputDir, $"{section.FileName}.md"), section.Content);
        docsTable.AddRow(section.Title, $"{section.Content.Length:N0} chars");
    }

    var fullMd = generator.GenerateMarkdown(project);
    File.WriteAllText(Path.Combine(outputDir, $"{project.ProjectName}_Full.md"), fullMd);

    var json = generator.GenerateJson(project);
    File.WriteAllText(schemaPath, json);
    docsTable.AddRow("JSON Schema", $"{json.Length:N0} chars");

    AnsiConsole.Write(docsTable);

    // Auto-generate diff report if previous snapshot exists
    if (File.Exists(previousPath))
    {
        try
        {
            var prevProject = MarkdownDocumentationGenerator.DeserializeProject(File.ReadAllText(previousPath));
            if (prevProject != null && prevProject.SourceHash != project.SourceHash)
            {
                var diffEngine = new ProjectDiffEngine();
                var diffReport = diffEngine.Compare(prevProject, project);

                if (diffReport.HasChanges)
                {
                    var diffGen = new DiffMarkdownGenerator(language!);
                    var diffMd = diffGen.Generate(diffReport);
                    var diffPath = Path.Combine(outputDir, $"{project.ProjectName}_DiffReport.md");
                    File.WriteAllText(diffPath, diffMd);

                    AnsiConsole.WriteLine();
                    DisplayDiffSummary(diffReport);
                }
                else
                {
                    AnsiConsole.MarkupLine("[grey]No structural changes since last extraction.[/]");
                }
            }
        }
        catch { /* Skip diff on deserialization errors */ }
    }

    hashCalc.SaveHash(projectPath, project.SourceHash);
    AnsiConsole.MarkupLine($"[green]✓[/] Output saved to: {outputDir}");

}, projectPathOption, languageOption, forceOption, ormOption, allOption, enrichOption);

rootCommand.AddCommand(extractCommand);

// ============================================================
// COMMAND: sync
// ============================================================

var syncCommand = new Command("sync", "Extract + upload documentation to PeopleWorks Copilot");
syncCommand.AddOption(apiUrlOption);
syncCommand.AddOption(tokenOption);
syncCommand.AddOption(userNameOption);
syncCommand.AddOption(resourceNameOption);
syncCommand.AddOption(projectPathOption);
syncCommand.AddOption(languageOption);
syncCommand.AddOption(forceOption);
syncCommand.AddOption(ormOption);
syncCommand.AddOption(allOption);
syncCommand.AddOption(enrichOption);

syncCommand.SetHandler(async (context) =>
{
    var apiUrl = context.ParseResult.GetValueForOption(apiUrlOption);
    var token = context.ParseResult.GetValueForOption(tokenOption);
    var userName = context.ParseResult.GetValueForOption(userNameOption);
    var resourceName = context.ParseResult.GetValueForOption(resourceNameOption);
    var projectPath = context.ParseResult.GetValueForOption(projectPathOption);
    var language = context.ParseResult.GetValueForOption(languageOption);
    var force = context.ParseResult.GetValueForOption(forceOption);
    var orm = context.ParseResult.GetValueForOption(ormOption);
    var all = context.ParseResult.GetValueForOption(allOption);
    var enrich = context.ParseResult.GetValueForOption(enrichOption);

    var config = ConfigHelper.Load();

    // --all: batch sync all configured projects
    if (all)
    {
        apiUrl ??= config.ApiUrl;
        token ??= config.Token;
        userName ??= config.UserName ?? "xaf-logic-explainer";

        if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token))
        {
            AnsiConsole.MarkupLine("[red]✗[/] API URL and Token are required. Configure with: xaflogic config");
            return;
        }

        if (config.Projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No projects configured. Add one with: [cyan]xaflogic projects add[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[blue]Syncing {config.Projects.Count} projects...[/]");
        AnsiConsole.WriteLine();

        var summary = new Table().Border(TableBorder.Rounded).Title("[blue]Multi-Project Sync[/]");
        summary.AddColumn("Project");
        summary.AddColumn("Resource");
        summary.AddColumn("Docs");
        summary.AddColumn("Time");
        summary.AddColumn("Status");

        foreach (var proj in config.Projects)
        {
            var projLang = proj.Language ?? config.Language ?? "es";
            var projOrm = proj.Orm ?? config.Orm;

            if (!Directory.Exists(proj.ProjectPath))
            {
                summary.AddRow(proj.Name, proj.ResourceName, "-", "-", "[red]PATH NOT FOUND[/]");
                continue;
            }

            try
            {
                var syncConfig = new SyncConfiguration
                {
                    ProjectPath = proj.ProjectPath,
                    CopilotApiBaseUrl = apiUrl,
                    CopilotApiToken = token,
                    ResourceName = proj.ResourceName,
                    UserName = userName,
                    LanguageCode = projLang,
                    ForceSync = force
                };

                var syncService = new IncrementalSyncService();
                var result = await syncService.SyncAsync(syncConfig, BuildExtractionOptions(projLang, projOrm));

                if (result.Success)
                {
                    summary.AddRow(proj.Name, proj.ResourceName,
                        result.DocumentsUploaded.ToString(),
                        $"{result.Duration.TotalSeconds:F1}s",
                        "[green]OK[/]");
                }
                else
                {
                    summary.AddRow(proj.Name, proj.ResourceName, "-", "-",
                        $"[red]{Markup.Escape(result.Message[..Math.Min(40, result.Message.Length)])}[/]");
                }
            }
            catch (Exception ex)
            {
                summary.AddRow(proj.Name, proj.ResourceName, "-", "-",
                    $"[red]{Markup.Escape(ex.Message[..Math.Min(40, ex.Message.Length)])}[/]");
            }
        }

        AnsiConsole.Write(summary);
        AnsiConsole.MarkupLine("[green]✓[/] Batch sync complete");
        return;
    }

    apiUrl ??= config.ApiUrl;
    token ??= config.Token;
    userName ??= config.UserName ?? "xaf-logic-explainer";
    resourceName ??= config.ResourceName;
    projectPath ??= config.ProjectPath;
    language ??= config.Language ?? "es";
    orm ??= config.Orm;

    if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token) ||
        string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(projectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] Missing required parameters. Configure with: xaflogic config");
        AnsiConsole.MarkupLine($"  API URL:      {(string.IsNullOrEmpty(apiUrl) ? "[red]missing[/]" : "[green]OK[/]")}");
        AnsiConsole.MarkupLine($"  Token:        {(string.IsNullOrEmpty(token) ? "[red]missing[/]" : "[green]OK[/]")}");
        AnsiConsole.MarkupLine($"  Resource:     {(string.IsNullOrEmpty(resourceName) ? "[red]missing[/]" : "[green]OK[/]")}");
        AnsiConsole.MarkupLine($"  Project Path: {(string.IsNullOrEmpty(projectPath) ? "[red]missing[/]" : "[green]OK[/]")}");
        return;
    }

    if (!Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Project directory not found: {projectPath}");
        return;
    }

    AnsiConsole.MarkupLine($"[blue]Project:[/]  {projectPath}");
    AnsiConsole.MarkupLine($"[blue]Resource:[/] {resourceName}");

    var singleSyncConfig = new SyncConfiguration
    {
        ProjectPath = projectPath,
        CopilotApiBaseUrl = apiUrl,
        CopilotApiToken = token,
        ResourceName = resourceName,
        UserName = userName,
        LanguageCode = language,
        ForceSync = force
    };

    SyncResult singleResult = null!;

    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Syncing to PeopleWorks Copilot...", async ctx =>
    {
        var syncService = new IncrementalSyncService();
        Func<ExtractedProject, Task>? enrichHook = null;
        if (enrich)
        {
            enrichHook = async (extractedProject) =>
            {
                await EnrichWithAi(extractedProject, config, language!);
            };
        }
        singleResult = await syncService.SyncAsync(singleSyncConfig, BuildExtractionOptions(language, orm), msg =>
        {
            ctx.Status(msg);
        }, enrichHook);
    });

    // Results
    if (singleResult.Success)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] Sync completed in {singleResult.Duration.TotalSeconds:F1}s");
    }
    else
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Sync failed: {Markup.Escape(singleResult.Message)}");
    }

    if (singleResult.UploadedDocuments.Count > 0)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Document");
        table.AddColumn("Size");
        table.AddColumn("Status");

        foreach (var doc in singleResult.UploadedDocuments)
        {
            var status = doc.Success ? "[green]OK[/]" : "[red]FAIL[/]";
            table.AddRow(doc.Title, $"{doc.ContentLength:N0} chars", status);
        }
        AnsiConsole.Write(table);
    }

    AnsiConsole.MarkupLine($"  Uploaded: {singleResult.DocumentsUploaded} | Failed: {singleResult.DocumentsFailed}");

    foreach (var err in singleResult.Errors)
        AnsiConsole.MarkupLine($"  [red]Error:[/] {Markup.Escape(err)}");
});

rootCommand.AddCommand(syncCommand);

// ============================================================
// COMMAND: status
// ============================================================

var statusCommand = new Command("status", "Show project hash and change detection status");
statusCommand.AddOption(projectPathOption);
statusCommand.AddOption(apiUrlOption);
statusCommand.AddOption(tokenOption);
statusCommand.AddOption(userNameOption);
statusCommand.AddOption(resourceNameOption);
statusCommand.AddOption(allOption);

statusCommand.SetHandler(async (projectPath, apiUrl, token, userName, resourceName, all) =>
{
    var config = ConfigHelper.Load();

    // --all: show status for all configured projects
    if (all)
    {
        if (config.Projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No projects configured. Add one with: [cyan]xaflogic projects add[/]");
            return;
        }

        var statusTable = new Table().Border(TableBorder.Rounded).Title("[blue]Multi-Project Status[/]");
        statusTable.AddColumn("Project");
        statusTable.AddColumn("Resource");
        statusTable.AddColumn("Hash");
        statusTable.AddColumn("Changed");

        var statusHashCalc = new ProjectHashCalculator();
        foreach (var proj in config.Projects)
        {
            if (!Directory.Exists(proj.ProjectPath))
            {
                statusTable.AddRow(proj.Name, proj.ResourceName, "[grey]?[/]", "[red]PATH NOT FOUND[/]");
                continue;
            }

            var projChanged = statusHashCalc.HasChanged(proj.ProjectPath);
            var projHash = statusHashCalc.GetCurrentHash(proj.ProjectPath);
            statusTable.AddRow(proj.Name, proj.ResourceName,
                projHash[..16] + "...",
                projChanged ? "[yellow]YES[/]" : "[green]NO[/]");
        }

        AnsiConsole.Write(statusTable);
        return;
    }

    projectPath ??= config.ProjectPath;
    apiUrl ??= config.ApiUrl;
    token ??= config.Token;
    userName ??= config.UserName ?? "xaf-logic-explainer";
    resourceName ??= config.ResourceName;

    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] --project is required. Set it with: xaflogic config --project <path>");
        return;
    }

    var hashCalc = new ProjectHashCalculator();
    var hasChanged = hashCalc.HasChanged(projectPath);
    var currentHash = hashCalc.GetCurrentHash(projectPath);
    var savedHash = hashCalc.GetSavedHash(projectPath);

    var csFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
        .ToArray();

    var xafmlFiles = Directory.GetFiles(projectPath, "*.xafml", SearchOption.AllDirectories)
        .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                    && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
        .ToArray();

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Property");
    table.AddColumn("Value");

    table.AddRow("Project", projectPath);
    table.AddRow("Source files (.cs)", csFiles.Length.ToString());
    table.AddRow("Model files (.xafml)", xafmlFiles.Length.ToString());
    table.AddRow("Current hash", currentHash[..16] + "...");
    table.AddRow("Saved hash", savedHash != null ? savedHash[..16] + "..." : "(none)");
    table.AddRow("Changed", hasChanged ? "[yellow]YES - sync needed[/]" : "[green]NO - up to date[/]");

    // Test Copilot connection if configured
    if (!string.IsNullOrEmpty(apiUrl) && !string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(resourceName))
    {
        try
        {
            var syncService = new IncrementalSyncService();
            var (connected, message) = await syncService.TestConnectionAsync(new SyncConfiguration
            {
                CopilotApiBaseUrl = apiUrl,
                CopilotApiToken = token,
                UserName = userName!,
                ResourceName = resourceName
            });
            table.AddRow("Copilot", connected ? $"[green]Connected[/] - {message}" : $"[red]Disconnected[/] - {message}");
        }
        catch (Exception ex)
        {
            table.AddRow("Copilot", $"[red]Error:[/] {ex.Message}");
        }
    }
    else
    {
        table.AddRow("Copilot", "[grey](not configured)[/]");
    }

    AnsiConsole.Write(table);

}, projectPathOption, apiUrlOption, tokenOption, userNameOption, resourceNameOption, allOption);

rootCommand.AddCommand(statusCommand);

// ============================================================
// COMMAND: chat
// ============================================================

var chatCommand = new Command("chat", "Interactive Q&A with PeopleWorks Copilot");
chatCommand.AddOption(apiUrlOption);
chatCommand.AddOption(tokenOption);
chatCommand.AddOption(userNameOption);
chatCommand.AddOption(resourceNameOption);

chatCommand.SetHandler(async (apiUrl, token, userName, resourceName) =>
{
    var config = ConfigHelper.Load();
    apiUrl ??= config.ApiUrl;
    token ??= config.Token;
    userName ??= config.UserName ?? "xaf-logic-explainer";
    resourceName ??= config.ResourceName;

    if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(resourceName))
    {
        AnsiConsole.MarkupLine("[red]✗[/] Missing required parameters. Configure with: xaflogic config");
        return;
    }

    using var client = new CopilotApiClient(apiUrl, token, userName!, resourceName);

    int sessionId = 0;
    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Starting chat session...", async ctx =>
    {
        var startResult = await client.StartChatAsync("XAF Logic CLI Chat");
        if (!startResult.Success)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to start chat: {Markup.Escape(startResult.Message ?? "Unknown error")}");
            return;
        }
        sessionId = startResult.ChatSessionId;
    });

    if (sessionId == 0) return;

    AnsiConsole.MarkupLine($"[green]✓[/] Chat session started (ID: {sessionId}). Type [grey]exit[/] to quit.");
    AnsiConsole.WriteLine();

    while (true)
    {
        var question = AnsiConsole.Prompt(new TextPrompt<string>("[blue]You>[/] ").AllowEmpty());
        if (string.IsNullOrWhiteSpace(question) || question.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        ChatMessageResponse? response = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Thinking...", async ctx =>
        {
            response = await client.SendChatMessageAsync(sessionId, question);
        });

        if (response?.Success == true)
            AnsiConsole.MarkupLine($"\n[green]Copilot>[/] {Markup.Escape(response.Response ?? "")}\n");
        else
            AnsiConsole.MarkupLine($"\n[red]Error:[/] {response?.Message ?? "No response"}\n");
    }

    AnsiConsole.MarkupLine("[grey]Chat session ended.[/]");

}, apiUrlOption, tokenOption, userNameOption, resourceNameOption);

rootCommand.AddCommand(chatCommand);

// ============================================================
// COMMAND: watch
// ============================================================

var watchCommand = new Command("watch", "Watch for file changes and auto-sync to PeopleWorks Copilot");
watchCommand.AddOption(apiUrlOption);
watchCommand.AddOption(tokenOption);
watchCommand.AddOption(userNameOption);
watchCommand.AddOption(resourceNameOption);
watchCommand.AddOption(projectPathOption);
watchCommand.AddOption(languageOption);
var debounceOption = new Option<int>("--debounce", () => 3, "Seconds to wait after last change before syncing");

watchCommand.AddOption(debounceOption);
watchCommand.AddOption(ormOption);
watchCommand.AddOption(allOption);

watchCommand.SetHandler(async (context) =>
{
    var apiUrl = context.ParseResult.GetValueForOption(apiUrlOption);
    var token = context.ParseResult.GetValueForOption(tokenOption);
    var userName = context.ParseResult.GetValueForOption(userNameOption);
    var resourceName = context.ParseResult.GetValueForOption(resourceNameOption);
    var projectPath = context.ParseResult.GetValueForOption(projectPathOption);
    var language = context.ParseResult.GetValueForOption(languageOption);
    var debounceSeconds = context.ParseResult.GetValueForOption(debounceOption);
    var orm = context.ParseResult.GetValueForOption(ormOption);
    var all = context.ParseResult.GetValueForOption(allOption);

    var config = ConfigHelper.Load();
    apiUrl ??= config.ApiUrl;
    token ??= config.Token;
    userName ??= config.UserName ?? "xaf-logic-explainer";

    if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token))
    {
        AnsiConsole.MarkupLine("[red]✗[/] API URL and Token are required. Configure with: xaflogic config");
        return;
    }

    if (debounceSeconds <= 0) debounceSeconds = 3;

    var cts = new CancellationTokenSource();
    var watchers = new List<FileSystemWatcher>();
    var syncLock = new SemaphoreSlim(1, 1);
    var syncCount = 0;

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // --all: watch all configured projects
    if (all)
    {
        if (config.Projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No projects configured. Add one with: [cyan]xaflogic projects add[/]");
            return;
        }

        AnsiConsole.Write(new FigletText("Watch Mode").Color(Color.Cyan1));
        AnsiConsole.MarkupLine($"[blue]Projects:[/] {config.Projects.Count} configured");
        AnsiConsole.MarkupLine($"[blue]Debounce:[/] {debounceSeconds}s");
        AnsiConsole.WriteLine();

        // Per-project debounce timers and sync configs
        var projectTimers = new Dictionary<string, Timer?>();
        var projectConfigs = new Dictionary<string, (SyncConfiguration SyncConfig, ExtractionOptions Options)>();

        foreach (var proj in config.Projects)
        {
            if (!Directory.Exists(proj.ProjectPath))
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {proj.Name}: path not found ({proj.ProjectPath})");
                continue;
            }

            var projLang = proj.Language ?? config.Language ?? "es";
            var projOrm = proj.Orm ?? config.Orm;

            var projSyncConfig = new SyncConfiguration
            {
                ProjectPath = proj.ProjectPath,
                CopilotApiBaseUrl = apiUrl,
                CopilotApiToken = token,
                ResourceName = proj.ResourceName,
                UserName = userName,
                LanguageCode = projLang,
                ForceSync = true
            };

            projectConfigs[proj.Name] = (projSyncConfig, BuildExtractionOptions(projLang, projOrm));
            projectTimers[proj.Name] = null;

            // Per-project sync trigger
            async void TriggerProjectSync(object? state)
            {
                if (cts.IsCancellationRequested) return;
                if (!await syncLock.WaitAsync(0)) return;

                try
                {
                    syncCount++;
                    var iteration = syncCount;
                    var (sc, opts) = projectConfigs[proj.Name];

                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[cyan]◆[/] [{Markup.Escape(proj.Name)}] Change detected - syncing... [grey](#{iteration})[/]");

                    var syncService = new IncrementalSyncService();
                    var result = await syncService.SyncAsync(sc, opts, msg =>
                    {
                        AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(msg)}[/]");
                    });

                    if (result.Success)
                        AnsiConsole.MarkupLine($"  [green]✓[/] [{Markup.Escape(proj.Name)}] Sync #{iteration} completed in {result.Duration.TotalSeconds:F1}s — {result.DocumentsUploaded} docs");
                    else
                    {
                        AnsiConsole.MarkupLine($"  [red]✗[/] [{Markup.Escape(proj.Name)}] Sync #{iteration} failed: {Markup.Escape(result.Message)}");
                        foreach (var err in result.Errors)
                            AnsiConsole.MarkupLine($"    [red]Error:[/] {Markup.Escape(err)}");
                    }

                    AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] [{Markup.Escape(proj.Name)}] Error: {Markup.Escape(ex.Message)}");
                    AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");
                }
                finally
                {
                    syncLock.Release();
                }
            }

            // Per-project file change handler with debounce
            void OnProjectFileChanged(object sender, FileSystemEventArgs e)
            {
                if (cts.IsCancellationRequested) return;

                var path = e.FullPath;
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                    path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    return;

                AnsiConsole.MarkupLine($"  [grey][{Markup.Escape(proj.Name)}] File changed: {Markup.Escape(Path.GetFileName(path))}[/]");
                projectTimers[proj.Name]?.Dispose();
                projectTimers[proj.Name] = new Timer(TriggerProjectSync, null, debounceSeconds * 1000, Timeout.Infinite);
            }

            // Setup watchers for this project
            void AddWatcher(string dir, string filter)
            {
                var w = new FileSystemWatcher(dir, filter)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                w.Changed += OnProjectFileChanged;
                w.Created += OnProjectFileChanged;
                w.Deleted += OnProjectFileChanged;
                w.Renamed += (s, e) => OnProjectFileChanged(s, e);
                watchers.Add(w);
            }

            AddWatcher(proj.ProjectPath, "*.cs");
            AddWatcher(proj.ProjectPath, "*.xafml");

            // Sibling platform projects
            var projParentDir = Directory.GetParent(proj.ProjectPath)?.FullName;
            if (projParentDir != null)
            {
                var projDirName = new DirectoryInfo(proj.ProjectPath).Name;
                foreach (var siblingDir in Directory.GetDirectories(projParentDir))
                {
                    var siblingName = new DirectoryInfo(siblingDir).Name;
                    if (siblingName == projDirName) continue;

                    var siblingXafml = Directory.GetFiles(siblingDir, "*.xafml", SearchOption.AllDirectories)
                        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                                 && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                        .ToArray();

                    if (siblingXafml.Length > 0)
                    {
                        AddWatcher(siblingDir, "*.xafml");
                        AnsiConsole.MarkupLine($"  [blue]{proj.Name}[/] sibling: {siblingName} ({siblingXafml.Length} xafml)");
                    }
                }
            }

            AnsiConsole.MarkupLine($"  [green]✓[/] {proj.Name} -> {proj.ResourceName}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓[/] Watching {watchers.Count} directories across {config.Projects.Count} projects");
        AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            foreach (var t in projectTimers.Values) t?.Dispose();
            foreach (var w in watchers) { w.EnableRaisingEvents = false; w.Dispose(); }
            syncLock.Dispose();
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[yellow]■[/] Watch mode stopped. Total syncs: {syncCount}");
        return;
    }

    // Single-project watch (existing behavior)
    resourceName ??= config.ResourceName;
    projectPath ??= config.ProjectPath;
    language ??= config.Language ?? "es";
    orm ??= config.Orm;

    if (string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(projectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] Missing required parameters. Configure with: xaflogic config");
        AnsiConsole.MarkupLine($"  API URL:      [green]OK[/]");
        AnsiConsole.MarkupLine($"  Token:        [green]OK[/]");
        AnsiConsole.MarkupLine($"  Resource:     {(string.IsNullOrEmpty(resourceName) ? "[red]missing[/]" : "[green]OK[/]")}");
        AnsiConsole.MarkupLine($"  Project Path: {(string.IsNullOrEmpty(projectPath) ? "[red]missing[/]" : "[green]OK[/]")}");
        return;
    }

    if (!Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Project directory not found: {projectPath}");
        return;
    }

    AnsiConsole.Write(new FigletText("Watch Mode").Color(Color.Cyan1));
    AnsiConsole.MarkupLine($"[blue]Project:[/]  {projectPath}");
    AnsiConsole.MarkupLine($"[blue]Resource:[/] {resourceName}");
    AnsiConsole.MarkupLine($"[blue]Debounce:[/] {debounceSeconds}s");
    AnsiConsole.MarkupLine($"[blue]Language:[/] {language}");
    AnsiConsole.WriteLine();

    var syncConfig = new SyncConfiguration
    {
        ProjectPath = projectPath,
        CopilotApiBaseUrl = apiUrl,
        CopilotApiToken = token,
        ResourceName = resourceName,
        UserName = userName,
        LanguageCode = language,
        ForceSync = true
    };

    var extractionOptions = BuildExtractionOptions(language, orm);
    var syncService = new IncrementalSyncService();
    Timer? debounceTimer = null;

    // Sync function with debounce lock
    async void TriggerSync(object? state)
    {
        if (cts.IsCancellationRequested) return;
        if (!await syncLock.WaitAsync(0)) return; // Skip if already syncing

        try
        {
            syncCount++;
            var iteration = syncCount;
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[cyan]◆[/] Change detected - syncing... [grey](#{iteration})[/]");

            var sw = Stopwatch.StartNew();
            var result = await syncService.SyncAsync(syncConfig, extractionOptions, msg =>
            {
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(msg)}[/]");
            });

            if (result.Success)
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] Sync #{iteration} completed in {result.Duration.TotalSeconds:F1}s — {result.DocumentsUploaded} docs ({result.UploadedDocuments.Sum(d => d.ContentLength):N0} chars)");
            }
            else
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] Sync #{iteration} failed: {Markup.Escape(result.Message)}");
                foreach (var err in result.Errors)
                    AnsiConsole.MarkupLine($"    [red]Error:[/] {Markup.Escape(err)}");
            }

            AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] Error: {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");
        }
        finally
        {
            syncLock.Release();
        }
    }

    // Debounce handler: reset timer on each file change
    void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (cts.IsCancellationRequested) return;

        // Exclude obj/ and bin/ directories
        var path = e.FullPath;
        if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
            path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            return;

        AnsiConsole.MarkupLine($"  [grey]File changed: {Markup.Escape(Path.GetFileName(path))}[/]");
        debounceTimer?.Dispose();
        debounceTimer = new Timer(TriggerSync, null, debounceSeconds * 1000, Timeout.Infinite);
    }

    // Watcher for .cs files in project directory
    var csWatcher = new FileSystemWatcher(projectPath, "*.cs")
    {
        IncludeSubdirectories = true,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        EnableRaisingEvents = true
    };
    csWatcher.Changed += OnFileChanged;
    csWatcher.Created += OnFileChanged;
    csWatcher.Deleted += OnFileChanged;
    csWatcher.Renamed += (s, e) => OnFileChanged(s, e);
    watchers.Add(csWatcher);

    // Watcher for .xafml files in project directory
    var xafmlWatcher = new FileSystemWatcher(projectPath, "*.xafml")
    {
        IncludeSubdirectories = true,
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
        EnableRaisingEvents = true
    };
    xafmlWatcher.Changed += OnFileChanged;
    xafmlWatcher.Created += OnFileChanged;
    xafmlWatcher.Deleted += OnFileChanged;
    xafmlWatcher.Renamed += (s, e) => OnFileChanged(s, e);
    watchers.Add(xafmlWatcher);

    // Watchers for sibling platform projects (Blazor.Server, Win)
    var parentDir = Directory.GetParent(projectPath)?.FullName;
    if (parentDir != null)
    {
        var projectDirName = new DirectoryInfo(projectPath).Name;
        foreach (var siblingDir in Directory.GetDirectories(parentDir))
        {
            var siblingName = new DirectoryInfo(siblingDir).Name;
            if (siblingName == projectDirName) continue;

            // Check if sibling has xafml files (platform project)
            var siblingXafml = Directory.GetFiles(siblingDir, "*.xafml", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                .ToArray();

            if (siblingXafml.Length > 0)
            {
                var siblingWatcher = new FileSystemWatcher(siblingDir, "*.xafml")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                siblingWatcher.Changed += OnFileChanged;
                siblingWatcher.Created += OnFileChanged;
                siblingWatcher.Deleted += OnFileChanged;
                siblingWatcher.Renamed += (s, e) => OnFileChanged(s, e);
                watchers.Add(siblingWatcher);

                AnsiConsole.MarkupLine($"[blue]Sibling:[/]  {siblingName} ({siblingXafml.Length} xafml files)");
            }
        }
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]✓[/] Watching {watchers.Count} directories for .cs and .xafml changes");
    AnsiConsole.MarkupLine("[grey]  Watching for changes... (Ctrl+C to stop)[/]");

    // Wait for cancellation
    try
    {
        await Task.Delay(Timeout.Infinite, cts.Token);
    }
    catch (OperationCanceledException)
    {
        // Expected on Ctrl+C
    }
    finally
    {
        debounceTimer?.Dispose();
        foreach (var w in watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        syncLock.Dispose();
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[yellow]■[/] Watch mode stopped. Total syncs: {syncCount}");
});

rootCommand.AddCommand(watchCommand);

// ============================================================
// COMMAND: diff
// ============================================================

var diffCommand = new Command("diff", "Show changes between current and previous extraction");
diffCommand.AddOption(projectPathOption);
diffCommand.AddOption(languageOption);
var previousFileOption = new Option<string?>("--previous", "Path to a previous JSON schema file to compare against");
diffCommand.AddOption(previousFileOption);

diffCommand.SetHandler(async (projectPath, language, previousFile) =>
{
    var config = ConfigHelper.Load();
    projectPath ??= config.ProjectPath;
    language ??= config.Language ?? "es";

    if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] --project is required. Set it with: xaflogic config --project <path>");
        return;
    }

    // Find schema file dynamically (project name comes from Core extractor)
    var outputDir = Path.Combine(projectPath, ".xaflogic-output");
    var schemaFiles = Directory.Exists(outputDir)
        ? Directory.GetFiles(outputDir, "*_Schema.json")
        : [];

    if (schemaFiles.Length == 0)
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No snapshots found. Run [cyan]xaflogic extract --force[/] first.");
        return;
    }

    var schemaPath = schemaFiles[0];
    var projectName = Path.GetFileName(schemaPath).Replace("_Schema.json", "");
    var defaultPreviousPath = Path.Combine(outputDir, $"{projectName}_Previous.json");

    previousFile ??= defaultPreviousPath;

    if (!File.Exists(previousFile))
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No previous snapshot found. Run [cyan]xaflogic extract --force[/] twice to generate a diff.");
        AnsiConsole.MarkupLine($"  Expected: {previousFile}");
        return;
    }

    if (!File.Exists(schemaPath))
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No current snapshot found. Run [cyan]xaflogic extract[/] first.");
        return;
    }

    AnsiConsole.MarkupLine($"[blue]Project:[/]  {projectPath}");
    AnsiConsole.MarkupLine($"[blue]Previous:[/] {Path.GetFileName(previousFile)}");
    AnsiConsole.MarkupLine($"[blue]Current:[/]  {Path.GetFileName(schemaPath)}");
    AnsiConsole.WriteLine();

    ExtractedProject? prevProject = null;
    ExtractedProject? currProject = null;

    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Comparing snapshots...", async ctx =>
    {
        prevProject = MarkdownDocumentationGenerator.DeserializeProject(File.ReadAllText(previousFile));
        currProject = MarkdownDocumentationGenerator.DeserializeProject(File.ReadAllText(schemaPath));
        await Task.CompletedTask;
    });

    if (prevProject == null || currProject == null)
    {
        AnsiConsole.MarkupLine("[red]✗[/] Failed to deserialize JSON snapshots.");
        return;
    }

    var diffEngine = new ProjectDiffEngine();
    var report = diffEngine.Compare(prevProject, currProject);

    if (!report.HasChanges)
    {
        AnsiConsole.MarkupLine("[green]✓[/] No structural changes between snapshots.");
        return;
    }

    // Display colored summary in console
    DisplayDiffSummary(report);

    // Save Markdown report
    var diffGen = new DiffMarkdownGenerator(language!);
    var diffMd = diffGen.Generate(report);
    Directory.CreateDirectory(outputDir);
    var diffPath = Path.Combine(outputDir, $"{projectName}_DiffReport.md");
    File.WriteAllText(diffPath, diffMd);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]✓[/] Diff report saved to: {diffPath}");

}, projectPathOption, languageOption, previousFileOption);

rootCommand.AddCommand(diffCommand);

// ============================================================
// COMMAND: agents
// ============================================================
var agentsCommand = new Command(
    "agents",
    "Generate AGENTS.md, CLAUDE.md and Copilot instructions so AI agents understand this XAF app");

var agentsOutputOption = new Option<string?>(
    "--output",
    "Where to write (default: the solution or repository root above the module)");
var agentsOnlyOption = new Option<string?>(
    "--only",
    "Limit the files written: agents, claude, copilot (comma-separated)");

agentsCommand.AddOption(projectPathOption);
agentsCommand.AddOption(languageOption);
agentsCommand.AddOption(forceOption);
agentsCommand.AddOption(ormOption);
agentsCommand.AddOption(allOption);
agentsCommand.AddOption(enrichOption);
agentsCommand.AddOption(agentsOutputOption);
agentsCommand.AddOption(agentsOnlyOption);

// InvocationContext rather than a typed handler: SetHandler tops out at eight parameters, and
// this command already sits at that boundary. sync and watch resolve options the same way.
agentsCommand.SetHandler(async (context) =>
{
    var agentsProjectPath = context.ParseResult.GetValueForOption(projectPathOption);
    var agentsLanguage = context.ParseResult.GetValueForOption(languageOption);
    var agentsForce = context.ParseResult.GetValueForOption(forceOption);
    var agentsOrm = context.ParseResult.GetValueForOption(ormOption);
    var agentsAll = context.ParseResult.GetValueForOption(allOption);
    var agentsEnrich = context.ParseResult.GetValueForOption(enrichOption);
    var agentsOutput = context.ParseResult.GetValueForOption(agentsOutputOption);
    var agentsOnly = context.ParseResult.GetValueForOption(agentsOnlyOption);

    var agentsConfig = ConfigHelper.Load();
    var agentsOptions = ParseAgentTargets(agentsOnly, agentsOutput);

    if (agentsAll)
    {
        if (agentsConfig.Projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]✗[/] No projects configured. Add one with: [cyan]xaflogic projects add[/]");
            return;
        }

        foreach (var configured in agentsConfig.Projects)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold blue]▸ {Markup.Escape(configured.Name)}[/]");

            await GenerateAgentFiles(
                configured.ProjectPath,
                configured.Language ?? agentsLanguage ?? agentsConfig.Language ?? "es",
                configured.Orm ?? agentsOrm,
                agentsForce,
                agentsEnrich,
                agentsOptions,
                agentsConfig);
        }

        return;
    }

    var singleProjectPath = agentsProjectPath ?? agentsConfig.ProjectPath;

    if (string.IsNullOrEmpty(singleProjectPath) || !Directory.Exists(singleProjectPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] --project is required. Set it with: [cyan]xaflogic config --project <path>[/]");
        return;
    }

    await GenerateAgentFiles(
        singleProjectPath,
        agentsLanguage ?? agentsConfig.Language ?? "es",
        agentsOrm ?? agentsConfig.Orm,
        agentsForce,
        agentsEnrich,
        agentsOptions,
        agentsConfig);
});

rootCommand.AddCommand(agentsCommand);

// ============================================================
// COMMAND: explain
// ============================================================
var explainCommand = new Command(
    "explain",
    "Generate a self-contained HTML page explaining this XAF application to a person");

var explainOutputOption = new Option<string?>(
    "--output",
    "File to write (default: <ProjectName>-explainer.html in the project directory)");
var explainOpenOption = new Option<bool>(
    "--open",
    "Open the page in the default browser when it is written");

explainCommand.AddOption(projectPathOption);
explainCommand.AddOption(languageOption);
explainCommand.AddOption(ormOption);
explainCommand.AddOption(enrichOption);
explainCommand.AddOption(explainOutputOption);
explainCommand.AddOption(explainOpenOption);

explainCommand.SetHandler(async (explainProject, explainLanguage, explainOrm, explainEnrich, explainOutput, explainOpen) =>
{
    var explainConfig = ConfigHelper.Load();
    var explainPath = explainProject ?? explainConfig.ProjectPath;

    if (string.IsNullOrEmpty(explainPath) || !Directory.Exists(explainPath))
    {
        AnsiConsole.MarkupLine("[red]✗[/] --project is required. Set it with: [cyan]xaflogic config --project <path>[/]");
        return;
    }

    var explainLang = explainLanguage ?? explainConfig.Language ?? "en";

    ExtractedProject explained = null!;
    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Reading the application...", async ctx =>
    {
        explained = new LogicExtractor().ExtractFromSourceDirectory(
            explainPath, BuildExtractionOptions(explainLang, explainOrm ?? explainConfig.Orm));
        await Task.CompletedTask;
    });

    if (explainEnrich)
    {
        await EnrichWithAi(explained, explainConfig, explainLang);
    }

    var html = new HtmlExplainerGenerator(ThisAssemblyVersion()).Generate(explained);
    var explainFile = explainOutput ?? Path.Combine(explainPath, $"{explained.ProjectName}-explainer.html");

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(explainFile))!);
    File.WriteAllText(explainFile, html);

    var explainActions = explained.Controllers.Sum(c => c.Actions.Count);
    var explainTable = new Table().Border(TableBorder.Rounded).AddColumn("Explains").AddColumn("");
    explainTable.AddRow("Entities", explained.Entities.Count.ToString());
    explainTable.AddRow("Controllers", explained.Controllers.Count.ToString());
    explainTable.AddRow("Actions", explainActions.ToString());
    // Counted where they are declared. Entities carry what they inherit so that a reader of one
    // is told the whole truth about it, but a count that follows the fold measures the depth of
    // the class hierarchy: one rule on an audit base would report as one rule per entity.
    explainTable.AddRow("Relationships",
        explained.Entities.Sum(e => e.Relationships.Count(r => r.InheritedFrom is null)).ToString());
    explainTable.AddRow("Rules",
        explained.Entities.Sum(e => e.ValidationRules.Count(r => r.InheritedFrom is null)
                                  + e.AppearanceRules.Count(r => r.InheritedFrom is null)).ToString());
    AnsiConsole.Write(explainTable);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(Path.GetFullPath(explainFile))}");
    AnsiConsole.MarkupLine($"[grey]{html.Length / 1024:N0} KB · one file, no dependencies — send it to anyone.[/]");

    if (explainOpen)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.GetFullPath(explainFile)) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Headless, or no handler registered. The path is already printed above.
            AnsiConsole.MarkupLine("[grey]Could not open a browser; the path is above.[/]");
        }
    }
}, projectPathOption, languageOption, ormOption, enrichOption, explainOutputOption, explainOpenOption);

rootCommand.AddCommand(explainCommand);

// ============================================================
// COMMAND: mcp
// ============================================================
var mcpCommand = new Command(
    "mcp",
    "Run as a Model Context Protocol server so any AI agent can query this XAF app live");

mcpCommand.AddOption(projectPathOption);
mcpCommand.AddOption(languageOption);
mcpCommand.AddOption(ormOption);
mcpCommand.AddOption(allOption);

mcpCommand.SetHandler(async (context) =>
{
    // Nothing may be written to stdout from here on: it is the JSON-RPC channel, and a single
    // stray character makes the client fail to parse the stream. That includes the Spectre banner
    // and every AnsiConsole call, so this handler reports problems on stderr instead.
    var mcpProjectPath = context.ParseResult.GetValueForOption(projectPathOption);
    var mcpLanguage = context.ParseResult.GetValueForOption(languageOption);
    var mcpOrm = context.ParseResult.GetValueForOption(ormOption);
    var mcpAll = context.ParseResult.GetValueForOption(allOption);

    var mcpConfig = ConfigHelper.Load();
    var mcpSources = new List<XafProjectSource>();

    if (mcpAll || (mcpProjectPath is null && mcpConfig.Projects.Count > 0))
    {
        mcpSources.AddRange(mcpConfig.Projects.Select(p => new XafProjectSource
        {
            Name = p.Name,
            Path = p.ProjectPath,
            Orm = p.Orm ?? mcpOrm ?? mcpConfig.Orm,
            Language = p.Language ?? mcpLanguage ?? mcpConfig.Language ?? "en",
        }));
    }

    if (mcpSources.Count == 0)
    {
        // Falling back to discovery matters for the plugin install path: a marketplace plugin
        // declares `xaflogic mcp` with no arguments, because it cannot know where anyone's module
        // lives. Finding it under the working directory is what makes that work with no setup.
        // Shared with the standalone server package, which is in exactly the same position.
        var resolvedPath = mcpProjectPath ?? mcpConfig.ProjectPath
            ?? XafModuleLocator.Locate(Directory.GetCurrentDirectory());

        if (string.IsNullOrEmpty(resolvedPath) || !Directory.Exists(resolvedPath))
        {
            await Console.Error.WriteLineAsync(
                "xaflogic mcp: no XAF project found. Pass --project <module path>, " +
                "set a default with `xaflogic config --project <path>`, " +
                "or run from a directory containing an XAF module.");
            context.ExitCode = 1;
            return;
        }

        mcpSources.Add(new XafProjectSource
        {
            Name = new DirectoryInfo(resolvedPath).Name,
            Path = resolvedPath,
            Orm = mcpOrm ?? mcpConfig.Orm,
            Language = mcpLanguage ?? mcpConfig.Language ?? "en",
        });
    }

    await McpServerRunner.RunStdioAsync(mcpSources);
});

rootCommand.AddCommand(mcpCommand);

// ============================================================
// COMMAND: catalog
// ============================================================
var catalogCommand = new Command(
    "catalog",
    "Manage the DevExpress ground-truth catalog, which tells your logic apart from the framework's");

var catalogBuildCommand = new Command(
    "build",
    "Read your licensed DevExpress installation and generate the catalog");
var catalogPathOption = new Option<string?>(
    "--dx-path",
    "DevExpress installation directory. Searched for automatically if omitted.");
catalogBuildCommand.AddOption(catalogPathOption);
var catalogSourcesOption = new Option<string?>(
    "--dx-sources",
    "DevExpress Components/Sources directory. Found beside the assemblies if omitted.");
catalogBuildCommand.AddOption(catalogSourcesOption);

catalogBuildCommand.SetHandler((dxPath, dxSources) =>
{
    var installation = DevExpressInstallation.Locate(dxPath, dxSources);

    if (installation is null)
    {
        AnsiConsole.MarkupLine("[red]✗[/] No DevExpress installation found.");
        AnsiConsole.MarkupLine("[grey]Pass [/][cyan]--dx-path <directory>[/][grey], or the folder containing DevExpress.ExpressApp*.dll[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]The catalog needs a licensed DevExpress installation. Everything else in[/]");
        AnsiConsole.MarkupLine("[grey]this tool works without one; the catalog only sharpens the output.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[blue]DevExpress:[/] {installation.Version}");
    AnsiConsole.MarkupLine($"[grey]{Markup.Escape(installation.AssemblyDirectory)}[/]");
    AnsiConsole.MarkupLine($"[grey]{installation.XafAssemblies.Count} XAF assemblies[/]");
    AnsiConsole.WriteLine();

    XafCatalog builtCatalog = null!;
    AnsiConsole.Status().Spinner(Spinner.Known.Dots).Start("Reading framework metadata...", ctx =>
    {
        builtCatalog = new CatalogBuilder().Build(installation);

        // Where a controller activates is decided in its constructor, which assembly metadata does
        // not carry. The sources do, when that optional component is installed.
        if (installation.SourceDirectory is { } dxSourceDirectory)
        {
            ctx.Status("Reading controller constructors from source...");
            new SourceTargetingReader().Apply(builtCatalog, dxSourceDirectory);
        }

        ControllerTargetingResolver.Resolve(builtCatalog);
    });

    var savedPath = XafCatalogStore.Save(builtCatalog);

    var catalogTable = new Table().Border(TableBorder.Rounded).AddColumn("Framework types").AddColumn("");
    catalogTable.AddRow("Attributes", builtCatalog.Attributes.Count.ToString());
    catalogTable.AddRow("Controllers", builtCatalog.Controllers.Count.ToString());
    catalogTable.AddRow("Model interfaces", builtCatalog.ModelInterfaces.Count.ToString());
    catalogTable.AddRow("Modules", builtCatalog.Modules.Count.ToString());
    AnsiConsole.Write(catalogTable);

    // Say how much of the framework's activation behaviour is actually known. Silence here reads
    // as "all of it", and without the sources it is almost none of it.
    var knownTargeting = builtCatalog.Controllers.Values.Count(c => c.TargetingSource == "sources");

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine(
        $"[grey]Know where {knownTargeting} of {builtCatalog.Controllers.Count} framework controllers activate.[/]");

    if (installation.SourceDirectory is null)
    {
        AnsiConsole.MarkupLine(
            "[yellow]![/] [grey]No DevExpress sources found. Install the source code component, or pass[/]");
        AnsiConsole.MarkupLine("[grey]  [/][cyan]--dx-sources <Components/Sources>[/][grey], to complete it.[/]");
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(savedPath)}");
    AnsiConsole.MarkupLine("[grey]Kept outside your repository: it is derived from licensed software.[/]");
    AnsiConsole.MarkupLine("[grey]Extraction picks it up automatically from now on.[/]");
}, catalogPathOption, catalogSourcesOption);

var catalogStatusCommand = new Command("status", "Show which catalog is in use");

catalogStatusCommand.SetHandler(() =>
{
    var existing = XafCatalogStore.LoadLatest();

    if (existing is null)
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No catalog. Extraction works without one, but cannot tell");
        AnsiConsole.MarkupLine("   framework controllers and attributes from the ones you wrote.");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("   Build one with: [cyan]xaflogic catalog build[/]");
        return;
    }

    AnsiConsole.MarkupLine($"[green]✓[/] DevExpress {existing.DevExpressVersion} — {existing.TypeCount} framework types");
    AnsiConsole.MarkupLine($"[grey]  generated {existing.GeneratedAt}[/]");
    AnsiConsole.MarkupLine($"[grey]  {XafCatalogStore.DefaultDirectory}[/]");

    foreach (var file in XafCatalogStore.List())
        AnsiConsole.MarkupLine($"[grey]  · {Markup.Escape(Path.GetFileName(file))}[/]");
});

catalogCommand.AddCommand(catalogBuildCommand);
catalogCommand.AddCommand(catalogStatusCommand);
rootCommand.AddCommand(catalogCommand);

// ============================================================
// HELPERS
// ============================================================

// Turns the --only and --output values into sink options.
static AgentFilesOptions ParseAgentTargets(string? only, string? output)
{
    if (string.IsNullOrWhiteSpace(only))
        return new AgentFilesOptions { OutputRoot = output };

    var requested = only
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.ToLowerInvariant())
        .ToHashSet();

    return new AgentFilesOptions
    {
        OutputRoot = output,
        WriteAgentsMd = requested.Contains("agents"),
        WriteClaudeMd = requested.Contains("claude"),
        WriteCopilotInstructions = requested.Contains("copilot"),
    };
}

// Extracts one project and writes the agent-facing context files.
static async Task GenerateAgentFiles(
    string projectPath,
    string language,
    string? orm,
    bool force,
    bool enrich,
    AgentFilesOptions options,
    CliConfig config)
{
    if (!Directory.Exists(projectPath))
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Not found: {Markup.Escape(projectPath)}");
        return;
    }

    var agentHashCalc = new ProjectHashCalculator();
    if (!force && !agentHashCalc.HasChanged(projectPath))
    {
        AnsiConsole.MarkupLine("[yellow]⊘[/] No source changes since the last run. Use [cyan]--force[/] to regenerate anyway.");
        return;
    }

    ExtractedProject agentProject = null!;
    await AnsiConsole.Status().Spinner(Spinner.Known.Dots).StartAsync("Reading the application...", async ctx =>
    {
        var agentExtractor = new LogicExtractor();
        agentProject = agentExtractor.ExtractFromSourceDirectory(projectPath, BuildExtractionOptions(language, orm));
        await Task.CompletedTask;
    });

    if (enrich)
    {
        await EnrichWithAi(agentProject, config, language);
    }

    var agentGenerator = new MarkdownDocumentationGenerator(language);
    var agentSections = agentGenerator.GenerateSections(agentProject);

    var agentSink = new AgentFilesSink(options, ThisAssemblyVersion());
    var agentResult = await agentSink.PublishAsync(agentProject, agentSections);

    if (!agentResult.Success)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {Markup.Escape(agentResult.Summary)}");
        return;
    }

    agentHashCalc.SaveHash(projectPath, agentProject.SourceHash);

    // What the agent now knows, stated as facts rather than file sizes -- the point of the command
    // is the knowledge, not the bytes.
    var agentActions = agentProject.Controllers.Sum(c => c.Actions.Count);
    var knows = new Table().Border(TableBorder.Rounded).AddColumn("Your agent now knows").AddColumn("");
    knows.AddRow("Business entities", agentProject.Entities.Count.ToString());
    knows.AddRow("Controllers", agentProject.Controllers.Count.ToString());
    knows.AddRow("Actions", agentActions.ToString());
    knows.AddRow("ORM", agentProject.OrmType);
    if (agentProject.Navigation.Count > 0)
        knows.AddRow("Navigation groups", agentProject.Navigation.Count.ToString());
    if (agentProject.ModelEditorInfo is { } modelInfo && modelInfo.Views.Count > 0)
        knows.AddRow("Model Editor views", modelInfo.Views.Count.ToString());
    AnsiConsole.Write(knows);

    AnsiConsole.WriteLine();
    foreach (var artifact in agentResult.Artifacts.Where(a =>
                 !a.Contains(AgentContextGenerator.DetailFolder, StringComparison.Ordinal)))
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(artifact)}");
    }

    var detailCount = agentResult.Artifacts.Count(a =>
        a.Contains(AgentContextGenerator.DetailFolder, StringComparison.Ordinal));
    if (detailCount > 0)
    {
        AnsiConsole.MarkupLine(
            $"[grey]+ {detailCount} detail files in {AgentContextGenerator.DetailFolder}/ (read on demand, not loaded every request)[/]");
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[grey]Ask your agent something only this codebase could answer.[/]");
}

// The tool version, stamped into generated files so a stale one can be identified.
static string ThisAssemblyVersion() =>
    System.Reflection.Assembly.GetExecutingAssembly().GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "0.9.0";

static ExtractionOptions BuildExtractionOptions(string language, string? orm = null)
{
    var options = new ExtractionOptions
    {
        IncludeSourceCode = true,
        IncludeMethodBodies = true,
        IncludeComments = true,
        LanguageCode = language
    };

    if (orm != null)
    {
        options.Orm = orm.ToLowerInvariant() switch
        {
            "xpo" => OrmType.Xpo,
            "efcore" or "ef" => OrmType.EfCore,
            _ => OrmType.Auto
        };
    }

    return options;
}

static void DisplayDiffSummary(ProjectDiffReport report)
{
    var s = report.Summary;

    AnsiConsole.Write(new Rule("[cyan]Change Report[/]").RuleStyle("grey"));
    AnsiConsole.MarkupLine($"[blue]Previous:[/] {report.PreviousHash[..16]}...  [blue]Current:[/] {report.CurrentHash[..16]}...");
    AnsiConsole.WriteLine();

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn("Category");
    table.AddColumn(new TableColumn("[green]+ Added[/]").Centered());
    table.AddColumn(new TableColumn("[red]- Removed[/]").Centered());
    table.AddColumn(new TableColumn("[yellow]~ Modified[/]").Centered());

    if (s.EntitiesAdded > 0 || s.EntitiesRemoved > 0 || s.EntitiesModified > 0)
        table.AddRow("Entities",
            s.EntitiesAdded > 0 ? $"[green]{s.EntitiesAdded}[/]" : "[grey]0[/]",
            s.EntitiesRemoved > 0 ? $"[red]{s.EntitiesRemoved}[/]" : "[grey]0[/]",
            s.EntitiesModified > 0 ? $"[yellow]{s.EntitiesModified}[/]" : "[grey]0[/]");

    if (s.ControllersAdded > 0 || s.ControllersRemoved > 0 || s.ControllersModified > 0)
        table.AddRow("Controllers",
            s.ControllersAdded > 0 ? $"[green]{s.ControllersAdded}[/]" : "[grey]0[/]",
            s.ControllersRemoved > 0 ? $"[red]{s.ControllersRemoved}[/]" : "[grey]0[/]",
            s.ControllersModified > 0 ? $"[yellow]{s.ControllersModified}[/]" : "[grey]0[/]");

    if (s.NavigationAdded > 0 || s.NavigationRemoved > 0 || s.NavigationModified > 0)
        table.AddRow("Navigation",
            s.NavigationAdded > 0 ? $"[green]{s.NavigationAdded}[/]" : "[grey]0[/]",
            s.NavigationRemoved > 0 ? $"[red]{s.NavigationRemoved}[/]" : "[grey]0[/]",
            s.NavigationModified > 0 ? $"[yellow]{s.NavigationModified}[/]" : "[grey]0[/]");

    if (s.SeedDataAdded > 0 || s.SeedDataRemoved > 0)
        table.AddRow("Seed Data",
            s.SeedDataAdded > 0 ? $"[green]{s.SeedDataAdded}[/]" : "[grey]0[/]",
            s.SeedDataRemoved > 0 ? $"[red]{s.SeedDataRemoved}[/]" : "[grey]0[/]",
            "[grey]0[/]");

    if (report.ModelEditorChanges?.HasChanges == true)
    {
        var me = report.ModelEditorChanges;
        table.AddRow("Model Editor (xafml)",
            me.AddedViews.Count + me.AddedBOClasses.Count > 0 ? $"[green]{me.AddedViews.Count + me.AddedBOClasses.Count}[/]" : "[grey]0[/]",
            me.RemovedViews.Count + me.RemovedBOClasses.Count > 0 ? $"[red]{me.RemovedViews.Count + me.RemovedBOClasses.Count}[/]" : "[grey]0[/]",
            me.ModifiedViews.Count + me.ModifiedBOClasses.Count > 0 ? $"[yellow]{me.ModifiedViews.Count + me.ModifiedBOClasses.Count}[/]" : "[grey]0[/]");
    }

    table.AddRow("",
        $"[green bold]{s.TotalAdded}[/]",
        $"[red bold]{s.TotalRemoved}[/]",
        $"[yellow bold]{s.TotalModified}[/]");

    AnsiConsole.Write(table);

    // Detail: entity changes
    if (report.EntityChanges.HasChanges)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Entity Details:[/]");
        foreach (var name in report.EntityChanges.Added)
            AnsiConsole.MarkupLine($"  [green]+[/] {Markup.Escape(name)} [green](new)[/]");
        foreach (var name in report.EntityChanges.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(name)} [red](removed)[/]");
        foreach (var entity in report.EntityChanges.Modified)
        {
            var changes = new List<string>();
            if (entity.AddedProperties.Count > 0) changes.Add($"+{entity.AddedProperties.Count} props");
            if (entity.RemovedProperties.Count > 0) changes.Add($"-{entity.RemovedProperties.Count} props");
            if (entity.ModifiedProperties.Count > 0) changes.Add($"~{entity.ModifiedProperties.Count} props");
            if (entity.AddedRelationships.Count > 0) changes.Add($"+{entity.AddedRelationships.Count} rels");
            if (entity.RemovedRelationships.Count > 0) changes.Add($"-{entity.RemovedRelationships.Count} rels");
            if (entity.AddedValidationRules.Count > 0) changes.Add($"+{entity.AddedValidationRules.Count} rules");
            if (entity.RemovedValidationRules.Count > 0) changes.Add($"-{entity.RemovedValidationRules.Count} rules");
            if (entity.DescriptionChange != null) changes.Add("description");
            if (entity.CaptionChange != null) changes.Add("caption");
            AnsiConsole.MarkupLine($"  [yellow]~[/] {Markup.Escape(entity.ClassName)} [grey]({string.Join(", ", changes)})[/]");
        }
    }

    // Detail: controller changes
    if (report.ControllerChanges.HasChanges)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Controller Details:[/]");
        foreach (var name in report.ControllerChanges.Added)
            AnsiConsole.MarkupLine($"  [green]+[/] {Markup.Escape(name)} [green](new)[/]");
        foreach (var name in report.ControllerChanges.Removed)
            AnsiConsole.MarkupLine($"  [red]-[/] {Markup.Escape(name)} [red](removed)[/]");
        foreach (var ctrl in report.ControllerChanges.Modified)
        {
            var changes = new List<string>();
            if (ctrl.AddedActions.Count > 0) changes.Add($"+{ctrl.AddedActions.Count} actions");
            if (ctrl.RemovedActions.Count > 0) changes.Add($"-{ctrl.RemovedActions.Count} actions");
            if (ctrl.ModifiedActions.Count > 0) changes.Add($"~{ctrl.ModifiedActions.Count} actions");
            if (ctrl.TargetObjectTypeChange != null) changes.Add("target changed");
            AnsiConsole.MarkupLine($"  [yellow]~[/] {Markup.Escape(ctrl.ClassName)} [grey]({string.Join(", ", changes)})[/]");
        }
    }
}

// ============================================================
// HELPER: AI Business Logic Enrichment
// ============================================================

static async Task EnrichWithAi(ExtractedProject project, CliConfig config, string language)
{
    if (project.Controllers.Count == 0)
    {
        AnsiConsole.MarkupLine("[grey]  No controllers to enrich.[/]");
        return;
    }

    var apiUrl = config.ApiUrl;
    var token = config.Token;
    var userName = config.UserName ?? "xaf-logic-explainer";
    var resourceName = config.ResourceName ?? "";

    if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(token))
    {
        AnsiConsole.MarkupLine("[yellow]  --enrich requires API credentials. Configure with: xaflogic config[/]");
        return;
    }

    AnsiConsole.MarkupLine("[grey]  Fetching AI provider credentials...[/]");
    AiProviderInfo? aiProvider = null;
    using (var apiClient = new CopilotApiClient(apiUrl, token, userName, resourceName))
    {
        aiProvider = await apiClient.GetAiProviderAsync();
    }

    if (aiProvider == null || string.IsNullOrEmpty(aiProvider.ApiKey))
    {
        AnsiConsole.MarkupLine("[yellow]  Could not retrieve AI provider. Skipping enrichment.[/]");
        return;
    }

    var model = aiProvider.Parameters?.GetValueOrDefault("model")?.ToString() ?? "gpt-4o-mini";
    var baseUrl = aiProvider.AiProviderBaseUrl ?? "https://api.openai.com/v1";

    AnsiConsole.MarkupLine($"[blue]AI:[/] {Markup.Escape(aiProvider.ProviderName ?? "OpenAI")} / {Markup.Escape(model)}");
    AnsiConsole.MarkupLine($"[blue]  Enriching {project.Controllers.Count} controllers...[/]");

    var openAiClient = new OpenAIClient(
        new System.ClientModel.ApiKeyCredential(aiProvider.ApiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
    var chatClient = openAiClient.GetChatClient(model).AsIChatClient();

    var enricher = new BusinessLogicEnricher(chatClient);
    await enricher.EnrichAsync(project, language, msg =>
    {
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(msg)}[/]");
    });

    var enrichedControllers = project.Controllers.Count(c => c.BusinessLogicSummary != null);
    var enrichedActions = project.Controllers.SelectMany(c => c.Actions).Count(a => a.BusinessLogicSummary != null);
    AnsiConsole.MarkupLine($"[green]  Enriched {enrichedControllers}/{project.Controllers.Count} controllers, {enrichedActions} actions[/]");
}

return await rootCommand.InvokeAsync(args);
