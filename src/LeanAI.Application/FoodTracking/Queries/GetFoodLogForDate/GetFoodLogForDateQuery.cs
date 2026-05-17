using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetFoodLogForDate;

public sealed record GetFoodLogForDateQuery(DateOnly Date) : IRequest<IReadOnlyList<FoodLogEntryDto>>;
