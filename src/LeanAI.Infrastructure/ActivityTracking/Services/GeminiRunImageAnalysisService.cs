using System.Text.Json;
using System.Text.Json.Serialization;
using LeanAI.Application.ActivityTracking.DTOs;
using LeanAI.Application.ActivityTracking.Services;
using Microsoft.Extensions.AI;

namespace LeanAI.Infrastructure.ActivityTracking.Services;

internal sealed class GeminiRunImageAnalysisService(IChatClient chatClient) : IRunImageAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] RequiredParameters = ["distance", "pace", "duration"];

    public async Task<IReadOnlyList<ActivityMetricDto>> AnalyzeAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, SystemInstruction),
            new ChatMessage(ChatRole.User, new AIContent[]
            {
                new DataContent(imageBytes, mimeType),
                new TextContent("Extract the running metrics from this screenshot.")
            })
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var text = response.Text ?? string.Empty;

        return ParseResponse(text);
    }

    internal static IReadOnlyList<ActivityMetricDto> ParseResponse(string raw)
    {
        var text = raw.Trim();

        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence    = text.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<GeminiMetricDto>>(text, JsonOptions);

            if (items is null || items.Count == 0)
                throw new InvalidOperationException("Could not extract run metrics from the provided image.");

            var paramNames = items
                .Select(i => i.ParameterName?.ToLowerInvariant())
                .ToHashSet();

            foreach (var required in RequiredParameters)
            {
                if (!paramNames.Contains(required))
                    throw new InvalidOperationException("Could not extract run metrics from the provided image.");
            }

            return items
                .Select(i => new ActivityMetricDto(
                    i.ParameterName ?? string.Empty,
                    i.Value         ?? string.Empty,
                    i.Unit          ?? string.Empty))
                .ToList();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Could not extract run metrics from the provided image.");
        }
    }

    private const string SystemInstruction =
        "You are a fitness data extractor. Analyze the running screenshot and extract metrics. " +
        "Reply with a JSON array only — no markdown fences, no extra text. " +
        "Each item must have 'parameter_name' (one of: distance, pace, duration), 'value', and 'unit'. " +
        "Example: [{\"parameter_name\":\"distance\",\"value\":\"5.2\",\"unit\":\"km\"}," +
        "{\"parameter_name\":\"pace\",\"value\":\"5:30\",\"unit\":\"min/km\"}," +
        "{\"parameter_name\":\"duration\",\"value\":\"28:36\",\"unit\":\"min\"}]";

    private sealed record GeminiMetricDto(
        [property: JsonPropertyName("parameter_name")] string? ParameterName,
        [property: JsonPropertyName("value")]          string? Value,
        [property: JsonPropertyName("unit")]           string? Unit);
}
