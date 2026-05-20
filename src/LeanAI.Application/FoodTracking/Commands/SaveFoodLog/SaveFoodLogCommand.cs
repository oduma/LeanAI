using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Commands.SaveFoodLog;

public sealed record SaveFoodLogCommand(DateOnly Date, IReadOnlyList<FoodItemDto> Items, bool IsImportMode) : IRequest;
