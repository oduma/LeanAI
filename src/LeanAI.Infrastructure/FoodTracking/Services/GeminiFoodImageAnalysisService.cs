using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeanAI.Application.FoodTracking.DTOs;
using LeanAI.Application.FoodTracking.Services;
using Microsoft.Extensions.AI;

namespace LeanAI.Infrastructure.FoodTracking.Services;

internal sealed class GeminiFoodImageAnalysisService(IChatClient chatClient) : IFoodImageAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<FoodItemDto>> AnalyzeImageAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, ImageSystemInstruction),
            new ChatMessage(ChatRole.User, new AIContent[]
            {
                new DataContent(imageBytes, mimeType),
                new TextContent("List all food items visible in this meal photo.")
            })
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return ParseResponse(response.Text ?? string.Empty);
    }

    public async Task<IReadOnlyList<FoodItemDto>> RecalculateCaloriesAsync(
        IReadOnlyList<FoodItemInputDto> items, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Recalculate the caloric content for the following food items:");
        foreach (var item in items)
            sb.AppendLine($"- {item.FoodItem}: {item.Quantity}");

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, TextSystemInstruction),
            new ChatMessage(ChatRole.User, sb.ToString())
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        return ParseResponse(response.Text ?? string.Empty);
    }

    internal static IReadOnlyList<FoodItemDto> ParseResponse(string raw)
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
            var items = JsonSerializer.Deserialize<List<GeminiFoodItemDto>>(text, JsonOptions);

            if (items is null || items.Count == 0)
                throw new InvalidOperationException("Could not extract food items from the provided image.");

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.FoodItem))
                    throw new InvalidOperationException("Could not extract food items from the provided image.");
                if (item.Calories is null)
                    throw new InvalidOperationException("Could not extract food items from the provided image.");
            }

            return items
                .Select(i => new FoodItemDto(
                    i.FoodItem  ?? string.Empty,
                    i.Quantity  ?? string.Empty,
                    i.Calories!.Value))
                .ToList();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Could not extract food items from the provided image.");
        }
    }

    private const string ImageSystemInstruction =
        "You are a nutrition data extractor. Analyze the meal photo and list all visible food items. " +
        "Reply with a JSON array only — no markdown fences, no extra text. " +
        "Each item must have 'food_item' (string), 'quantity' (string, e.g. '150g' or '1 cup'), " +
        "and 'calories' (number in kcal). " +
        "Example: [{\"food_item\":\"Grilled Chicken\",\"quantity\":\"150g\",\"calories\":248}]";

    private const string TextSystemInstruction =
        "You are a nutrition data calculator. Given a list of food items and quantities, " +
        "calculate the caloric content for each. " +
        "Reply with a JSON array only — no markdown fences, no extra text. " +
        "Each item must have 'food_item' (string), 'quantity' (string), and 'calories' (number in kcal). " +
        "Example: [{\"food_item\":\"Pasta\",\"quantity\":\"250g\",\"calories\":310}]";

    private sealed record GeminiFoodItemDto(
        [property: JsonPropertyName("food_item")] string? FoodItem,
        [property: JsonPropertyName("quantity")]  string? Quantity,
        [property: JsonPropertyName("calories")]  double? Calories);
}
