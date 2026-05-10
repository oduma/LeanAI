using System.Text.Json;
using System.Text.Json.Serialization;
using LeanAI.Application.WeightManagement.DTOs;
using LeanAI.Application.WeightManagement.Enums;
using LeanAI.Application.WeightManagement.Queries.ValidateGoal;
using LeanAI.Application.WeightManagement.Services;
using LeanAI.Domain.WeightManagement.Enums;
using Microsoft.Extensions.AI;

namespace LeanAI.Infrastructure.Services;

internal sealed class GeminiGoalValidationService(IChatClient chatClient) : IAIGoalValidationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GoalValidationResult> ValidateAsync(ValidateGoalQuery query, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(query);

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, SystemInstruction),
            new ChatMessage(ChatRole.User, prompt)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
        var text = response.Text ?? string.Empty;

        return ParseResponse(text);
    }

    private const string SystemInstruction =
        "You are a trusted medical advisor and fitness specialist evaluating weight-loss goals for medical safety. " +
        "Reply with valid JSON only — no markdown fences, no extra text: " +
        """{"status":"SAFE"|"WARNING"|"DANGER","message":"Your plain-text assessment (2-3 sentences max)."} """ +
        "Status rules: SAFE = medically sound and achievable within recognised safe weight-loss guidelines. " +
        "WARNING = aggressive goal, achievable only with extreme effort, carries meaningful health risk. " +
        "DANGER = medically unsafe or physically impossible in the given timeframe.";

    private static string BuildPrompt(ValidateGoalQuery q)
    {
        var genderLabel  = q.Gender == Gender.Male ? "Male" : "Female";
        var heightLabel  = $"{q.HeightCm:F1} cm";
        var startLabel   = $"{q.StartingWeightKg:F1} kg";
        var targetLabel  = $"{q.TargetWeightKg:F1} kg";
        var periodLabel  = q.TargetPeriod switch
        {
            TargetPeriod.ThreeMonths => "3 months",
            TargetPeriod.SixMonths   => "6 months",
            TargetPeriod.OneYear     => "1 year",
            _                        => q.TargetPeriod.ToString()
        };

        return $"Profile: {genderLabel}, {q.Age} years old, {heightLabel}, current weight {startLabel}. " +
               $"Goal: Reach {targetLabel} in {periodLabel}.";
    }

    internal static GoalValidationResult ParseResponse(string raw)
    {
        var text = raw.Trim();

        // Strip markdown code fences if present
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            var lastFence    = text.LastIndexOf("```");
            if (firstNewline >= 0 && lastFence > firstNewline)
                text = text[(firstNewline + 1)..lastFence].Trim();
        }

        try
        {
            var dto = JsonSerializer.Deserialize<GeminiResponseDto>(text, JsonOptions);
            if (dto is null)
                return new GoalValidationResult(GoalValidationStatus.Danger, raw);

            var status = dto.Status?.ToUpperInvariant() switch
            {
                "SAFE"    => GoalValidationStatus.Safe,
                "WARNING" => GoalValidationStatus.Warning,
                "DANGER"  => GoalValidationStatus.Danger,
                _         => GoalValidationStatus.Danger
            };

            return new GoalValidationResult(status, dto.Message ?? string.Empty);
        }
        catch (JsonException)
        {
            return new GoalValidationResult(GoalValidationStatus.Danger, raw);
        }
    }

    private sealed record GeminiResponseDto(
        [property: JsonPropertyName("status")]  string? Status,
        [property: JsonPropertyName("message")] string? Message);
}
