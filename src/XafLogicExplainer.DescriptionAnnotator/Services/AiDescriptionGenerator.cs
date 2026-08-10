using Microsoft.Extensions.AI;

namespace XafLogicExplainer.DescriptionAnnotator.Services;

/// <summary>
/// Generates class and property description suggestions using a chat-completion provider.
/// </summary>
public class AiDescriptionGenerator
{
    private readonly IChatClient _chatClient;

    /// <summary>
    /// Creates an AI description generator.
    /// </summary>
    /// <param name="chatClient">Configured chat client implementation.</param>
    public AiDescriptionGenerator(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    /// <summary>
    /// Generate descriptions for all missing items using AI.
    /// </summary>
    public async Task GenerateDescriptionsAsync(List<MissingDescriptionItem> items, string languageCode = "es", Action<string>? onProgress = null)
    {
        var total = items.Count;
        var current = 0;

        // Group by class to batch context
        var grouped = items.GroupBy(i => i.ClassName);

        foreach (var group in grouped)
        {
            var classItems = group.ToList();
            var classItem = classItems.FirstOrDefault(i => i.MemberType == "class");
            var propItems = classItems.Where(i => i.MemberType == "property").ToList();

            // Generate class description first (if missing)
            if (classItem != null)
            {
                current++;
                onProgress?.Invoke($"  [{current}/{total}] {classItem.ClassName} (class)...");
                classItem.SuggestedDescription = await GenerateClassDescriptionAsync(classItem.Context, languageCode);
            }

            // Generate property descriptions in batch
            if (propItems.Count > 0)
            {
                var batchResult = await GeneratePropertyDescriptionsBatchAsync(propItems, languageCode);
                foreach (var (item, desc) in batchResult)
                {
                    current++;
                    onProgress?.Invoke($"  [{current}/{total}] {item.ClassName}.{item.MemberName}...");
                    item.SuggestedDescription = desc;
                }
            }
        }
    }

    /// <summary>
    /// Generates one class description from class context.
    /// </summary>
    private async Task<string?> GenerateClassDescriptionAsync(ClassContext ctx, string lang)
    {
        var prompt = BuildClassPrompt(ctx, lang);
        try
        {
            var response = await _chatClient.GetResponseAsync(prompt);
            return CleanResponse(response.Text);
        }
        catch (Exception ex)
        {
            return $"[AI Error: {ex.Message}]";
        }
    }

    /// <summary>
    /// Generates descriptions for a property batch and maps responses back to items.
    /// </summary>
    private async Task<List<(MissingDescriptionItem item, string description)>> GeneratePropertyDescriptionsBatchAsync(
        List<MissingDescriptionItem> items, string lang)
    {
        var results = new List<(MissingDescriptionItem, string)>();

        // Batch up to 10 properties per request for efficiency
        foreach (var batch in items.Chunk(10))
        {
            var prompt = BuildPropertyBatchPrompt(batch, lang);
            try
            {
                var response = await _chatClient.GetResponseAsync(prompt);
                var descriptions = ParseBatchResponse(response.Text, batch);

                foreach (var (item, desc) in descriptions)
                    results.Add((item, desc));
            }
            catch (Exception ex)
            {
                foreach (var item in batch)
                    results.Add((item, $"[AI Error: {ex.Message}]"));
            }
        }

        return results;
    }

    /// <summary>
    /// Builds prompt text for class description generation.
    /// </summary>
    private static string BuildClassPrompt(ClassContext ctx, string lang)
    {
        var langInstruction = lang == "es"
            ? "Responde en espanol."
            : "Respond in English.";

        return $"""
            Generate a concise [Description] attribute value for this XAF business class.
            The description should explain what this class represents in business terms (not technical).
            Maximum 100 characters. {langInstruction}

            Class: {ctx.ClassName}
            Base type: {ctx.BaseType}
            Navigation group: {ctx.NavigationGroup ?? "none"}
            Display property: {ctx.DefaultProperty ?? "none"}
            Properties: {string.Join(", ", ctx.PropertyNames.Take(15))}
            Relationships: {string.Join(", ", ctx.RelatedEntities.Take(10))}

            Return ONLY the description text, no quotes, no attribute syntax, no explanation.
            """;
    }

    /// <summary>
    /// Builds prompt text for batched property description generation.
    /// </summary>
    private static string BuildPropertyBatchPrompt(MissingDescriptionItem[] batch, string lang)
    {
        var langInstruction = lang == "es"
            ? "Responde en espanol."
            : "Respond in English.";

        var ctx = batch[0].Context;
        var propList = string.Join("\n", batch.Select(b =>
        {
            var extra = "";
            if (b.Context.IsComputed) extra += " [computed]";
            if (b.Context.IsCollection) extra += " [collection]";
            if (b.Context.Size != null) extra += $" [size:{b.Context.Size}]";
            return $"- {b.MemberName} ({b.TypeName}){extra}";
        }));

        return $"""
            Generate concise [Description] attribute values for these properties of class {ctx.ClassName}.
            Each description should explain the property's business purpose (not technical).
            Maximum 80 characters each. {langInstruction}

            Class: {ctx.ClassName} ({ctx.BaseType})
            Navigation: {ctx.NavigationGroup ?? "none"}
            All properties: {string.Join(", ", ctx.PropertyNames.Take(15))}

            Properties needing descriptions:
            {propList}

            Return one line per property in format: PropertyName|Description text
            No quotes, no attribute syntax.
            """;
    }

    /// <summary>
    /// Parses batch response lines into property/description mappings.
    /// </summary>
    private static List<(MissingDescriptionItem item, string description)> ParseBatchResponse(
        string? responseText, MissingDescriptionItem[] batch)
    {
        var results = new List<(MissingDescriptionItem, string)>();
        if (string.IsNullOrEmpty(responseText))
        {
            foreach (var item in batch)
                results.Add((item, "[No AI response]"));
            return results;
        }

        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lineMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var pipeIndex = line.IndexOf('|');
            if (pipeIndex <= 0) continue;

            var name = line[..pipeIndex].Trim().TrimStart('-', ' ');
            var desc = line[(pipeIndex + 1)..].Trim();
            lineMap[name] = desc;
        }

        foreach (var item in batch)
        {
            if (item.MemberName != null && lineMap.TryGetValue(item.MemberName, out var desc))
                results.Add((item, desc));
            else
                results.Add((item, "[Could not parse AI response]"));
        }

        return results;
    }

    /// <summary>
    /// Normalizes single-line model output into plain attribute text.
    /// </summary>
    private static string? CleanResponse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return text.Trim().Trim('"').Trim();
    }
}
