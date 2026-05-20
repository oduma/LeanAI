using LeanAI.Application.FoodTracking.DTOs;
using MediatR;

namespace LeanAI.Application.FoodTracking.Queries.GetActivityCaloriesForDate;

public sealed record GetActivityCaloriesForDateQuery(DateOnly Date)
    : IRequest<IReadOnlyList<ActivityCaloryLogDto>>;
