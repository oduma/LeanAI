namespace LeanAI.Application.WeightManagement.DTOs;

public sealed record TrendsDataDto(
    DateOnly                      FirstDate,
    DateOnly                      LastDate,
    IReadOnlyList<WeightPointDto> IdealSeries,
    IReadOnlyList<WeightPointDto> ActualSeries,
    IReadOnlyList<WeightPointDto> IdealWeeklyAverages,
    IReadOnlyList<WeightPointDto> ActualWeeklyAverages,
    IReadOnlyList<WeeklyDeltaDto> WeeklyDeltas
);
