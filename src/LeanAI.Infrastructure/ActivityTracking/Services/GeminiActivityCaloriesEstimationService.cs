using System.Text.Json;
using LeanAI.Application.ActivityTracking.Services;
using Microsoft.Extensions.AI;

namespace LeanAI.Infrastructure.ActivityTracking.Services;

internal sealed class GeminiActivityCaloriesEstimationService(IChatClient chatClient)
    : IActivityCaloriesEstimationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<double>> EstimateAsync(
        IReadOnlyList<string> descriptions,
        CancellationToken     ct = default)
    {
        if (descriptions.Count == 0)
            return Array.Empty<double>();

        var descriptionsJson = JsonSerializer.Serialize(descriptions);

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, SystemInstruction),
            new ChatMessage(ChatRole.User,
                $"Estimate calories burned for each of these activity descriptions: {descriptionsJson}")
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var text     = response.Text ?? string.Empty;

        return ParseResponse(text, descriptions.Count);
    }

    internal static IReadOnlyList<double> ParseResponse(string raw, int expectedCount)
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
            var values = JsonSerializer.Deserialize<List<double>>(text, JsonOptions);
            if (values is null || values.Count == 0)
                return Enumerable.Repeat(0.0, expectedCount).ToList();

            // Pad or trim to match expectedCount
            while (values.Count < expectedCount)
                values.Add(0.0);

            return values.Take(expectedCount).ToList();
        }
        catch (JsonException)
        {
            return Enumerable.Repeat(0.0, expectedCount).ToList();
        }
    }

    private const string SystemInstruction =
        "You are a fitness calorie estimator. Given a list of activity descriptions, estimate the calories burned for each. " +
        "Reply with a JSON array of numbers only — no markdown fences, no extra text. " +
        "One number per description, in the same order. " +
        "Example for [\"5km run in 28 minutes\", \"30 minute swim\"]: [300, 250]";
}
