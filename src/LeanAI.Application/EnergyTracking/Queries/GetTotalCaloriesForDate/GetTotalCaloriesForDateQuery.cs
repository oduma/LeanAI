using MediatR;

namespace LeanAI.Application.EnergyTracking.Queries.GetTotalCaloriesForDate;

public sealed record GetTotalCaloriesForDateQuery(DateOnly Date) : IRequest<double?>;
