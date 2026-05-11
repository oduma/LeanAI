namespace LeanAI.Application.WeightManagement.DTOs;

public sealed record ImportSummaryDto(
    int      TotalDatesFound,
    int      IdealDatesImported,
    int      DatesWithWeightsFound,
    int      WeightRowsImported,
    int      Failed,
    DateOnly FirstDate,
    DateOnly LastDate
);
