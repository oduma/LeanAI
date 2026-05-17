using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.RecalculateCalories;

public sealed record RecalculateCaloriesCommand(IReadOnlyList<FoodItemInputDto> Items)
    : IRequest<IReadOnlyList<FoodItemDto>>;
