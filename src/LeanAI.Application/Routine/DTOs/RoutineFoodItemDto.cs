namespace LeanAI.Application.Routine.DTOs;

public sealed record RoutineFoodItemDto(
    Guid    Id,
    string  Description,
    string? Quantity,
    double  Calories
);
