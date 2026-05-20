namespace LeanAI.Application.FoodTracking.DTOs;

public sealed record RoutineItemDto(
    Guid    Id,
    string  SourceType,
    string  Description,
    string? Quantity,
    double  Calories
);
