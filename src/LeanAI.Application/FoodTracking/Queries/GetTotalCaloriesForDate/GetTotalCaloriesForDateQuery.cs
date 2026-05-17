using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetTotalCaloriesForDate;

public sealed record GetTotalCaloriesForDateQuery(DateOnly Date) : IRequest<double>;
