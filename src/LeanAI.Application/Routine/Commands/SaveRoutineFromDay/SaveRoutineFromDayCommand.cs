using LeanAI.Application.Routine.DTOs;
using MediatR;

namespace LeanAI.Application.Routine.Commands.SaveRoutineFromDay;

public record SaveRoutineFromDayCommand(
    IReadOnlyList<RoutineFoodItemDto>     FoodItems,
    IReadOnlyList<RoutineActivityItemDto> ActivityItems
) : IRequest;
