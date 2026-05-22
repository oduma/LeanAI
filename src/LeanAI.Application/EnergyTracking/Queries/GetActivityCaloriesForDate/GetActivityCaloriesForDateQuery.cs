using LeanAI.Application.EnergyTracking.DTOs;
using MediatR;

namespace LeanAI.Application.EnergyTracking.Queries.GetActivityCaloriesForDate;

public sealed record GetActivityCaloriesForDateQuery(DateOnly Date)
    : IRequest<IReadOnlyList<ActivityEnergyLogDto>>;
