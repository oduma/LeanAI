namespace LeanAI.Application.Routine.DTOs;

public sealed record RoutineActivityItemDto(
    Guid   Id,
    string Description,
    double Calories
);
