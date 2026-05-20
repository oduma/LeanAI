namespace LeanAI.Application.ActivityTracking.DTOs;

public sealed record RunImportResultDto(
    string ActivityText,
    double CaloriesBurned,
    IReadOnlyList<ActivityMetricDto> Metrics);
