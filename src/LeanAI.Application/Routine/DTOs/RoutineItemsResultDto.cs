namespace LeanAI.Application.Routine.DTOs;

public sealed record RoutineItemsResultDto(
    IReadOnlyList<RoutineFoodItemDto>     FoodItems,
    IReadOnlyList<RoutineActivityItemDto> ActivityItems
);
