using System.Text;
using Microsoft.Extensions.AI;
using XafLogicExplainer.Core.Models;

namespace XafLogicExplainer.CopilotSync.Services;

/// <summary>
/// Enriches extracted controllers with AI-generated business logic summaries.
/// </summary>
public class BusinessLogicEnricher
{
    private readonly IChatClient _chatClient;

    public BusinessLogicEnricher(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Enriches all controllers in the project with business logic summaries.
    /// </summary>
    public async Task EnrichAsync(
        ExtractedProject project,
        string languageCode = "es",
        Action<string>? onProgress = null)
    {
        var total = project.Controllers.Count;
        var current = 0;

        foreach (var controller in project.Controllers)
        {
            current++;
            onProgress?.Invoke($"[{current}/{total}] Enriching {controller.ClassName}...");

            try
            {
                if (controller.Actions.Count > 0)
                {
                    await EnrichActionsAsync(controller, project, languageCode);
                }

                controller.BusinessLogicSummary = await GenerateControllerSummaryAsync(
                    controller, project, languageCode);

                var enrichedActions = controller.Actions.Count(a => a.BusinessLogicSummary != null);
                onProgress?.Invoke($"  OK: {controller.ClassName} ({enrichedActions} actions enriched)");
            }
            catch (Exception ex)
            {
                onProgress?.Invoke($"  WARN: {controller.ClassName} - {ex.Message}");
            }
        }
    }

    private async Task EnrichActionsAsync(
        ExtractedController controller,
        ExtractedProject project,
        string languageCode)
    {
        // Process in batches of 3 actions to stay within token limits
        foreach (var batch in controller.Actions.Chunk(3))
        {
            var prompt = BuildActionBatchPrompt(controller, batch.ToList(), project, languageCode);
            var response = await _chatClient.GetResponseAsync(prompt);
            var summaries = ParseActionSummaries(response.Text, batch.ToList());

            foreach (var action in batch)
            {
                if (summaries.TryGetValue(action.ActionId, out var summary))
                    action.BusinessLogicSummary = summary;
            }
        }
    }

    private async Task<string?> GenerateControllerSummaryAsync(
        ExtractedController controller,
        ExtractedProject project,
        string languageCode)
    {
        var prompt = BuildControllerSummaryPrompt(controller, project, languageCode);
        var response = await _chatClient.GetResponseAsync(prompt);
        return string.IsNullOrWhiteSpace(response.Text) ? null : response.Text.Trim();
    }

    private static string BuildActionBatchPrompt(
        ExtractedController controller,
        List<ExtractedAction> actions,
        ExtractedProject project,
        string languageCode)
    {
        var langInstruction = languageCode == "es"
            ? "Responde completamente en espanol."
            : "Respond entirely in English.";

        var entityContext = BuildEntityContext(controller, project);

        var actionDetails = new StringBuilder();
        foreach (var action in actions)
        {
            actionDetails.AppendLine($"### Action: {action.ActionId}");
            actionDetails.AppendLine($"- Type: {action.ActionType}");
            if (action.Caption != null) actionDetails.AppendLine($"- Caption: {action.Caption}");
            if (action.Category != null) actionDetails.AppendLine($"- Category: {action.Category}");
            if (action.ConfirmationMessage != null)
                actionDetails.AppendLine($"- Confirmation: {action.ConfirmationMessage}");
            if (action.ExecuteMethodBody != null)
            {
                actionDetails.AppendLine("- Execute method body:");
                actionDetails.AppendLine("```csharp");
                var lines = action.ExecuteMethodBody.Split('\n');
                foreach (var line in lines.Take(200))
                    actionDetails.AppendLine(line);
                if (lines.Length > 200)
                    actionDetails.AppendLine($"// ... {lines.Length - 200} more lines");
                actionDetails.AppendLine("```");
            }
            actionDetails.AppendLine();
        }

        var helperMethods = new StringBuilder();
        foreach (var method in controller.Methods.Where(m =>
            !controller.Actions.Any(a => a.ExecuteMethodName == m.Name)))
        {
            if (string.IsNullOrEmpty(method.Body)) continue;
            var bodyLines = method.Body.Split('\n');
            if (bodyLines.Length > 80) continue;
            helperMethods.AppendLine($"### Helper: {method.Name}({string.Join(", ", method.Parameters)}) -> {method.ReturnType}");
            helperMethods.AppendLine("```csharp");
            helperMethods.AppendLine(method.Body);
            helperMethods.AppendLine("```");
            helperMethods.AppendLine();
        }

        return $"""
            You are a senior business analyst documenting a DevExpress XAF application.
            Analyze the following XAF controller actions and explain what EACH action does in
            detailed business terms. {langInstruction}

            For each action, explain:
            1. What business operation it performs (calculations, validations, entity creation/modification)
            2. What data it reads, transforms, or writes
            3. Any business rules or conditions it enforces
            4. The workflow/sequence of operations
            5. What entities and relationships it affects

            Be DETAILED (3-8 sentences per action). Focus on BUSINESS LOGIC, not technical XAF mechanics.
            Do NOT say "this action executes a method" - explain WHAT the method DOES.

            ## Controller: {controller.ClassName}
            - Base type: {controller.BaseControllerType}
            - Target entity: {controller.TargetObjectType ?? "N/A"}
            - Target view: {controller.TargetViewType ?? "N/A"}
            - Referenced entities: {string.Join(", ", controller.ReferencedEntities)}

            {entityContext}

            ## Actions to analyze:
            {actionDetails}

            {(helperMethods.Length > 0 ? $"## Helper methods called by actions:\n{helperMethods}" : "")}

            Return your response in this EXACT format, one block per action:
            ACTION_ID|
            Detailed summary paragraph here...
            |||

            Use ACTION_ID| as the header and ||| as the block terminator.
            One detailed paragraph per action. No markdown formatting inside the summary.
            """;
    }

    private static string BuildControllerSummaryPrompt(
        ExtractedController controller,
        ExtractedProject project,
        string languageCode)
    {
        var langInstruction = languageCode == "es"
            ? "Responde completamente en espanol."
            : "Respond entirely in English.";

        var actionSummaries = new StringBuilder();
        foreach (var action in controller.Actions)
        {
            actionSummaries.AppendLine($"- {action.Caption ?? action.ActionId}: {action.BusinessLogicSummary ?? "(no summary)"}");
        }

        var entityContext = BuildEntityContext(controller, project);

        return $"""
            You are a senior business analyst writing functional documentation for a DevExpress XAF application.
            Write a comprehensive business logic summary for this controller. {langInstruction}

            The summary should be 2-4 paragraphs covering:
            1. The overall purpose and scope of this controller in the application
            2. What business process or workflow it implements
            3. How its actions work together to accomplish the business goal
            4. What entities it reads/creates/modifies and the business rules it enforces
            5. Any important validations, calculations, or state transitions

            ## Controller: {controller.ClassName}
            - Base type: {controller.BaseControllerType}
            - Target entity: {controller.TargetObjectType ?? "N/A"}
            - Target view: {controller.TargetViewType ?? "N/A"}
            - Referenced entities: {string.Join(", ", controller.ReferencedEntities)}

            {entityContext}

            ## Actions in this controller:
            {actionSummaries}

            Write a detailed, multi-paragraph summary. No bullet points, no markdown headers.
            Focus on WHAT the controller does in business terms, not HOW it does it technically.
            """;
    }

    private static string BuildEntityContext(
        ExtractedController controller,
        ExtractedProject project)
    {
        var relevantEntities = project.Entities
            .Where(e => controller.ReferencedEntities.Contains(e.ClassName)
                        || e.ClassName == controller.TargetObjectType)
            .ToList();

        if (relevantEntities.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("## Relevant entity context:");
        foreach (var entity in relevantEntities)
        {
            sb.AppendLine($"### {entity.ClassName}");
            if (!string.IsNullOrEmpty(entity.Description))
                sb.AppendLine($"Description: {entity.Description}");
            var props = entity.Properties.Where(p => !p.IsCollection)
                .Take(20)
                .Select(p => $"{p.Name} ({p.TypeName}){(p.IsComputed ? " [computed]" : "")}{(p.IsRequired ? " [required]" : "")}");
            sb.AppendLine($"Properties: {string.Join(", ", props)}");
            if (entity.Relationships.Count > 0)
            {
                var rels = entity.Relationships.Select(r => $"{r.PropertyName}->{r.RelatedEntity} ({r.Type})");
                sb.AppendLine($"Relationships: {string.Join(", ", rels)}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static Dictionary<string, string> ParseActionSummaries(
        string? responseText, List<ExtractedAction> actions)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(responseText)) return result;

        var blocks = responseText.Split("|||", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var trimmed = block.Trim();
            var pipeIndex = trimmed.IndexOf('|');
            if (pipeIndex <= 0) continue;

            var actionId = trimmed[..pipeIndex].Trim();
            var summary = trimmed[(pipeIndex + 1)..].Trim();

            if (!string.IsNullOrEmpty(summary))
                result[actionId] = summary;
        }

        return result;
    }
}
