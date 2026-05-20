using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.SaveRoutineFromDay;

public record SaveRoutineFromDayCommand(IReadOnlyList<RoutineItemDto> Items) : IRequest;
