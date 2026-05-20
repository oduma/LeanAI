namespace LeanAI.Application.ActivityTracking.DTOs;

public sealed record RunActivityRowDto(
    string ActivityText,
    double Calories,
    bool   IsRunRow,
    IReadOnlyList<ActivityMetricDto>? Metrics = null);
