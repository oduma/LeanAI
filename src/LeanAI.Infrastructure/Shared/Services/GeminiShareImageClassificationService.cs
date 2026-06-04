using LeanAI.Application.Shared;
using LeanAI.Application.Shared.Services;
using Microsoft.Extensions.AI;

namespace LeanAI.Infrastructure.Shared.Services;

internal sealed class GeminiShareImageClassificationService(IChatClient chatClient)
    : IShareImageClassificationService
{
    private const string SystemInstruction =
        "You are an image classifier for a fitness tracking app. " +
        "Classify the image into exactly one category: " +
        "\"food\" — the image shows food, a meal, ingredients, or a restaurant menu; " +
        "\"run\" — the image is a screenshot from a fitness or running app showing metrics such as distance, pace, duration, or calories burned; " +
        "\"unknown\" — the image is neither of the above. " +
        "Respond with exactly one lowercase word: food, run, or unknown. No other text.";

    public async Task<ShareImageType> ClassifyAsync(
        byte[] imageBytes, string mimeType, CancellationToken ct = default)
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.System, SystemInstruction),
            new ChatMessage(ChatRole.User, new AIContent[]
            {
                new DataContent(imageBytes, mimeType),
                new TextContent("Classify this image.")
            })
        };

        try
        {
            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            return ParseResponse(response.Text ?? string.Empty);
        }
        catch
        {
            return ShareImageType.Unknown;
        }
    }

    internal static ShareImageType ParseResponse(string raw)
        => raw.Trim().ToLowerInvariant() switch
        {
            "food" => ShareImageType.Food,
            "run"  => ShareImageType.Run,
            _      => ShareImageType.Unknown
        };
}
