using Microsoft.Extensions.AI;
using OpenAI;
using XafLogicExplainer.CopilotSync.Ai;
using XafLogicExplainer.CopilotSync.Models;
using XafLogicExplainer.CopilotSync.Services;
using XafLogicExplainer.DescriptionAnnotator.Services;

// ============================================================
// XAF Description Annotator CLI
// Scans XAF projects for missing [Description] attributes,
// generates suggestions using AI, and optionally applies them.
// ============================================================

var mode = args.Length > 0 ? args[0] : "help";
var projectPath = GetArg(args, "--project") ?? GetArg(args, "-p");
var outputPath = GetArg(args, "--output") ?? GetArg(args, "-o");
var language = GetArg(args, "--lang") ?? "es";

// PeopleWorks Copilot connection (for AI provider credentials)
var copilotBaseUrl = GetArg(args, "--copilot-url")
    ?? Environment.GetEnvironmentVariable("COPILOT_API_URL")
    ?? "https://copilotapi.peopleworksservices.com";
var copilotToken = GetArg(args, "--copilot-token")
    ?? Environment.GetEnvironmentVariable("COPILOT_API_TOKEN")
    ?? "";
// No default here on purpose. A hardcoded resource name means anyone who runs this
// without configuring it silently reads and writes against someone else's resource.
var copilotResource = GetArg(args, "--copilot-resource")
    ?? Environment.GetEnvironmentVariable("COPILOT_RESOURCE")
    ?? "";
var copilotUser = GetArg(args, "--copilot-user")
    ?? Environment.GetEnvironmentVariable("COPILOT_USER")
    ?? "xaf-logic-explainer";

// The AI provider, on the same terms as the main CLI: a key given here, then the environment,
// then the PeopleWorks Copilot account above. That account used to be the only route, which
// made this tool unusable by anyone who did not have one.
var aiApiKey = GetArg(args, "--api-key");
var aiBaseUrl = GetArg(args, "--ai-base-url");
var aiModel = GetArg(args, "--ai-model");

// XAF base types to scan
var baseTypes = new[] { "XPCustomObject", "BaseObject", "XPObject", "XPLiteObject", "PermissionPolicyUser" };

Console.WriteLine("=== XAF Description Annotator ===");
Console.WriteLine();

switch (mode)
{
    case "scan":
        RunScan(projectPath, baseTypes);
        break;
    case "generate":
        await RunGenerate(projectPath, baseTypes, language, outputPath);
        break;
    case "apply":
        await RunApply(projectPath, baseTypes, language, outputPath);
        break;
    default:
        ShowHelp();
        break;
}

// ============================================================
// MODE: SCAN - Find missing [Description] attributes
// ============================================================
void RunScan(string? path, string[] baseTypeNames)
{
    if (!ValidatePath(path)) return;

    Console.WriteLine($"Scanning: {path}");
    Console.WriteLine();

    var scanner = new DescriptionScanner();
    var result = scanner.Scan(path!, baseTypeNames);

    Console.WriteLine("=== SCAN RESULTS ===");
    Console.WriteLine($"  Total classes:      {result.TotalClasses}");
    Console.WriteLine($"  Total properties:   {result.TotalProperties}");
    Console.WriteLine($"  Missing on classes: {result.MissingClassDescriptions}");
    Console.WriteLine($"  Missing on props:   {result.MissingPropertyDescriptions}");
    Console.WriteLine();

    if (result.MissingItems.Count == 0)
    {
        Console.WriteLine("All classes and properties have [Description] attributes!");
        return;
    }

    // Group by class
    var grouped = result.MissingItems.GroupBy(i => i.ClassName);
    foreach (var group in grouped.OrderBy(g => g.Key))
    {
        var classItem = group.FirstOrDefault(i => i.MemberType == "class");
        var classMarker = classItem != null ? " [class needs description]" : "";
        Console.WriteLine($"  {group.Key}{classMarker}");

        foreach (var prop in group.Where(i => i.MemberType == "property"))
        {
            Console.WriteLine($"    - {prop.MemberName} ({prop.TypeName}) [line {prop.LineNumber}]");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Total missing: {result.MissingItems.Count}");
    Console.WriteLine("Run with 'generate' mode to create AI-suggested descriptions.");
}

// ============================================================
// MODE: GENERATE - Scan + generate AI descriptions + report
// ============================================================
async Task RunGenerate(string? path, string[] baseTypeNames, string lang, string? output)
{
    if (!ValidatePath(path)) return;

    Console.WriteLine($"Scanning: {path}");
    var scanner = new DescriptionScanner();
    var result = scanner.Scan(path!, baseTypeNames);

    if (result.MissingItems.Count == 0)
    {
        Console.WriteLine("No missing [Description] attributes found!");
        return;
    }

    Console.WriteLine($"Found {result.MissingItems.Count} items missing [Description].");
    Console.WriteLine();

    // Get AI provider from PeopleWorks Copilot
    var chatClient = await CreateChatClient();
    if (chatClient == null) return;

    // Generate descriptions
    Console.WriteLine("Generating AI descriptions...");
    var generator = new AiDescriptionGenerator(chatClient);
    await generator.GenerateDescriptionsAsync(result.MissingItems, lang, msg => Console.WriteLine(msg));

    Console.WriteLine();

    // Generate report
    var report = SourceFileModifier.GenerateReport(result, result.MissingItems);

    // Save or display report
    var reportPath = output ?? Path.Combine(Path.GetDirectoryName(path!)!, "DescriptionAnnotator_Report.md");
    File.WriteAllText(reportPath, report);
    Console.WriteLine($"Report saved to: {reportPath}");

    // Summary
    var successful = result.MissingItems.Count(i =>
        !string.IsNullOrEmpty(i.SuggestedDescription) && !i.SuggestedDescription.StartsWith("["));
    var errors = result.MissingItems.Count(i =>
        i.SuggestedDescription?.StartsWith("[") == true);

    Console.WriteLine();
    Console.WriteLine("=== GENERATION SUMMARY ===");
    Console.WriteLine($"  Descriptions generated: {successful}");
    Console.WriteLine($"  Errors: {errors}");
    Console.WriteLine();
    Console.WriteLine("Run with 'apply' mode to write descriptions to source files.");
}

// ============================================================
// MODE: APPLY - Scan + generate + write to source files
// ============================================================
async Task RunApply(string? path, string[] baseTypeNames, string lang, string? output)
{
    if (!ValidatePath(path)) return;

    Console.WriteLine($"Scanning: {path}");
    var scanner = new DescriptionScanner();
    var result = scanner.Scan(path!, baseTypeNames);

    if (result.MissingItems.Count == 0)
    {
        Console.WriteLine("No missing [Description] attributes found!");
        return;
    }

    Console.WriteLine($"Found {result.MissingItems.Count} items missing [Description].");
    Console.WriteLine();

    // Get AI provider
    var chatClient = await CreateChatClient();
    if (chatClient == null) return;

    // Generate descriptions
    Console.WriteLine("Generating AI descriptions...");
    var generator = new AiDescriptionGenerator(chatClient);
    await generator.GenerateDescriptionsAsync(result.MissingItems, lang, msg => Console.WriteLine(msg));
    Console.WriteLine();

    // Save report first
    var report = SourceFileModifier.GenerateReport(result, result.MissingItems);
    var reportPath = output ?? Path.Combine(Path.GetDirectoryName(path!)!, "DescriptionAnnotator_Report.md");
    File.WriteAllText(reportPath, report);
    Console.WriteLine($"Report saved to: {reportPath}");

    // Apply to source files
    Console.WriteLine();
    Console.WriteLine("Applying [Description] attributes to source files...");
    var modifier = new SourceFileModifier();
    var modifiedFiles = modifier.Apply(result.MissingItems);

    Console.WriteLine();
    Console.WriteLine("=== APPLY RESULTS ===");
    Console.WriteLine($"  Files modified: {modifiedFiles.Count}");
    foreach (var file in modifiedFiles)
    {
        Console.WriteLine($"    - {Path.GetFileName(file)}");
    }

    var applied = result.MissingItems.Count(i =>
        !string.IsNullOrEmpty(i.SuggestedDescription) && !i.SuggestedDescription.StartsWith("["));
    Console.WriteLine($"  Descriptions applied: {applied}");
    Console.WriteLine();
    Console.WriteLine("Done! Review the changes and commit when satisfied.");
}

// ============================================================
// HELPERS
// ============================================================

async Task<IChatClient?> CreateChatClient()
{
    var resolved = await AiClientResolver.ResolveAsync(new AiClientRequest
    {
        ApiKey = aiApiKey,
        BaseUrl = aiBaseUrl,
        Model = aiModel,
        Copilot = new SyncConfiguration
        {
            CopilotApiBaseUrl = copilotBaseUrl,
            CopilotApiToken = copilotToken,
            ResourceName = copilotResource,
            UserName = copilotUser,
        },
    });

    if (!resolved.Succeeded)
    {
        Console.WriteLine(resolved.Problem ?? AiClientResolver.NothingConfigured);
        return null;
    }

    Console.WriteLine($"  Provider: {resolved.ProviderName}");
    Console.WriteLine($"  Model: {resolved.Model}");
    Console.WriteLine();

    return resolved.Client;
}

bool ValidatePath(string? path)
{
    if (string.IsNullOrEmpty(path))
    {
        Console.WriteLine("ERROR: --project <path> is required.");
        Console.WriteLine("Example: dotnet run -- scan --project \"C:\\Path\\To\\Module\"");
        return false;
    }

    if (!Directory.Exists(path))
    {
        Console.WriteLine($"ERROR: Directory not found: {path}");
        return false;
    }

    return true;
}

void ShowHelp()
{
    Console.WriteLine("Usage: dotnet run -- <command> --project <path> [options]");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  scan       Scan for missing [Description] attributes (no AI needed)");
    Console.WriteLine("  generate   Scan + generate AI descriptions + save report");
    Console.WriteLine("  apply      Scan + generate + write [Description] to source files");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --project, -p <path>      Path to XAF module directory (required)");
    Console.WriteLine("  --output, -o <path>       Output path for report file");
    Console.WriteLine("  --lang <code>             Language for descriptions: es (default) or en");
    Console.WriteLine("  --copilot-url <url>       PeopleWorks Copilot API URL");
    Console.WriteLine("  --copilot-token <token>   Copilot API token (or env COPILOT_API_TOKEN)");
    Console.WriteLine("  --copilot-resource <name> Copilot resource name (or env COPILOT_RESOURCE)");
    Console.WriteLine("  --copilot-user <name>     Copilot user name (or env COPILOT_USER)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run -- scan --project \"C:\\MyProject\\Module\"");
    Console.WriteLine("  dotnet run -- generate -p \"C:\\MyProject\\Module\" --lang es");
    Console.WriteLine("  dotnet run -- apply -p \"C:\\MyProject\\Module\" -o report.md");
}

static string? GetArg(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    if (index >= 0 && index + 1 < args.Length)
        return args[index + 1];
    return null;
}
